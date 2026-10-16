using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Entities;
using GroupFinance.Core.Enums;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Expenses;

public sealed class ExpenseValidationException(string message) : Exception(message);

public sealed class ExpenseService(GroupFinanceDbContext db, FinancialTransactionService financialTransactions)
{
    private static readonly string[] PaymentMethods = ["Cash", "Bank", "Online Transfer", "Other"];
    private static readonly string[] FundAccountTypes = ["Cash", "Bank", "Other"];

    public async Task<IReadOnlyCollection<ExpenseCategoryResponse>> ListCategoriesAsync(long groupId, bool includeInactive, CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);
        var query = db.ExpenseCategories.AsNoTracking().Include(x => x.Group).Where(x => x.GroupId == groupId);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        var rows = await query.OrderBy(x => x.Name).ToArrayAsync(ct);
        return rows.Select(MapCategory).ToArray();
    }

    public async Task<ExpenseCategoryResponse?> GetCategoryAsync(long id, CancellationToken ct)
    {
        var row = await db.ExpenseCategories.AsNoTracking().Include(x => x.Group).SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : MapCategory(row);
    }

    public async Task<ExpenseCategoryResponse> CreateCategoryAsync(CreateExpenseCategoryRequest request, long userId, CancellationToken ct)
    {
        await EnsureGroupAsync(request.GroupId, ct);
        var name = NormalizeRequired(request.Name, "Category name", 100);
        if (await db.ExpenseCategories.AnyAsync(x => x.GroupId == request.GroupId && x.Name == name, ct))
            throw new ExpenseValidationException("Another expense category already uses this name.");

        var now = DateTime.UtcNow;
        var entity = new ExpenseCategory
        {
            GroupId = request.GroupId,
            Name = name,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            CreatedByUserId = userId
        };
        db.ExpenseCategories.Add(entity);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        { throw new ExpenseValidationException("Another expense category already uses this name."); }
        return await GetCategoryAsync(entity.Id, ct) ?? throw new InvalidOperationException("The expense category was created but could not be loaded.");
    }

    public async Task<ExpenseCategoryResponse?> UpdateCategoryAsync(long id, UpdateExpenseCategoryRequest request, long userId, CancellationToken ct)
    {
        var entity = await db.ExpenseCategories.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        await EnsureGroupAsync(entity.GroupId, ct);
        var name = NormalizeRequired(request.Name, "Category name", 100);
        if (await db.ExpenseCategories.AnyAsync(x => x.Id != id && x.GroupId == entity.GroupId && x.Name == name, ct))
            throw new ExpenseValidationException("Another expense category already uses this name.");
        entity.Name = name;
        entity.IsActive = request.IsActive;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedByUserId = userId;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        { throw new ExpenseValidationException("Another expense category already uses this name."); }
        return await GetCategoryAsync(id, ct);
    }

    public async Task<IReadOnlyCollection<ExpenseResponse>> ListExpensesAsync(long groupId, DateTime? fromDate, DateTime? toDate, long? categoryId, string? status, CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);
        if (categoryId.HasValue && !await db.ExpenseCategories.AnyAsync(x => x.Id == categoryId.Value && x.GroupId == groupId, ct))
            throw new ExpenseValidationException("The selected expense category does not belong to the configured group.");

        var query = db.Expenses.AsNoTracking()
            .Include(x => x.Group)
            .Include(x => x.ExpenseCategory)
            .Include(x => x.FinancialTransaction)
            .ThenInclude(x => x!.Details)
            .ThenInclude(x => x.FinancialAccount)
            .Where(x => x.GroupId == groupId);
        if (fromDate.HasValue) query = query.Where(x => x.ExpenseDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.ExpenseDate < toDate.Value.Date.AddDays(1));
        if (categoryId.HasValue) query = query.Where(x => x.ExpenseCategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        var rows = await query.OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.Id).Take(1000).ToArrayAsync(ct);
        return rows.Select(MapExpense).ToArray();
    }

    public async Task<ExpenseResponse?> GetExpenseAsync(long id, CancellationToken ct)
    {
        var row = await db.Expenses.AsNoTracking()
            .Include(x => x.Group)
            .Include(x => x.ExpenseCategory)
            .Include(x => x.FinancialTransaction)
            .ThenInclude(x => x!.Details)
            .ThenInclude(x => x.FinancialAccount)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : MapExpense(row);
    }

    public async Task<ExpenseResponse> CreateExpenseAsync(CreateExpenseRequest request, long userId, CancellationToken ct)
    {
        ValidateAmount(request.Amount);
        var paymentMethod = NormalizePaymentMethod(request.PaymentMethod);
        var description = NormalizeRequired(request.Description, "Description", 1000);
        await EnsureGroupAsync(request.GroupId, ct);
        if (request.ExpenseDate == default) throw new ExpenseValidationException("Expense date is required.");

        var executionStrategy = db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);

            var category = await db.ExpenseCategories.SingleOrDefaultAsync(x => x.Id == request.ExpenseCategoryId && x.GroupId == request.GroupId, ct)
                ?? throw new ExpenseValidationException("The selected expense category does not exist or belongs to another group.");
            if (!category.IsActive) throw new ExpenseValidationException("Inactive expense categories cannot be used for new expenses.");

            var paymentAccount = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.Id == request.FinancialAccountId && x.GroupId == request.GroupId && x.IsActive, ct)
                ?? throw new ExpenseValidationException("The selected payment account does not exist, is inactive, or belongs to another group.");
            if (!FundAccountTypes.Contains(paymentAccount.AccountType, StringComparer.OrdinalIgnoreCase))
                throw new ExpenseValidationException("The payment account must be a Cash, Bank, or Other fund account.");

            var balance = await GetAccountBalanceAsync(paymentAccount.Id, ct);
            if (balance < request.Amount)
                throw new ExpenseValidationException($"Insufficient available balance in the selected payment account. Available balance is {balance:N2}.");

            var now = DateTime.UtcNow;
            var expense = new Expense
            {
                GroupId = request.GroupId,
                ExpenseCategoryId = request.ExpenseCategoryId,
                ExpenseNumber = $"TMP-{Guid.NewGuid():N}"[..30],
                ExpenseDate = request.ExpenseDate,
                Amount = RoundMoney(request.Amount),
                PaymentMethod = paymentMethod,
                Description = description,
                ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
                Status = "Posted",
                CreatedAtUtc = now,
                CreatedByUserId = userId
            };
            db.Expenses.Add(expense);
            await db.SaveChangesAsync(ct);
            expense.ExpenseNumber = $"EXP-{expense.Id:00000000}";

            var expenseAccount = await EnsureExpenseAccountAsync(request.GroupId, category, userId, ct);
            var posted = await financialTransactions.CreatePostedInCurrentTransactionAsync(
                new CreateFinancialTransactionRequest(
                    request.GroupId,
                    null,
                    request.ExpenseDate,
                    FinancialTransactionTypes.Expense,
                    description,
                    "EXPENSE",
                    expense.Id,
                    expense.ExpenseNumber,
                    [
                        new FinancialTransactionLineRequest(expenseAccount.Id, expense.Amount, 0m, $"Expense - {category.Name}"),
                        new FinancialTransactionLineRequest(paymentAccount.Id, 0m, expense.Amount, "Expense paid")
                    ]), userId, ct);

            expense.FinancialTransactionId = posted.Id;
            expense.ModifiedAtUtc = DateTime.UtcNow;
            expense.ModifiedByUserId = userId;
            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            return await GetExpenseAsync(expense.Id, ct) ?? throw new InvalidOperationException("The expense was posted but could not be loaded.");
        });
    }

    private async Task<FinancialAccount> EnsureExpenseAccountAsync(long groupId, ExpenseCategory category, long userId, CancellationToken ct)
    {
        var code = $"EXP-{category.Id:00000000}";
        var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.GroupId == groupId && x.AccountCode == code, ct);
        if (account is not null)
        {
            if (!account.IsActive) throw new ExpenseValidationException("The expense category's financial account is inactive.");
            if (!account.AccountType.Equals("Expense", StringComparison.OrdinalIgnoreCase))
                throw new ExpenseValidationException("The expense category's financial account has an invalid account type.");
            return account;
        }
        account = new FinancialAccount
        {
            GroupId = groupId,
            AccountCode = code,
            AccountName = $"Expense - {category.Name}",
            AccountType = "Expense",
            IsSystemAccount = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        db.FinancialAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }

    private async Task<decimal> GetAccountBalanceAsync(long accountId, CancellationToken ct)
        => await db.TransactionDetails.AsNoTracking()
            .Where(x => x.FinancialAccountId == accountId && x.FinancialTransaction.Status == "Posted")
            .SumAsync(x => (decimal?)(x.DebitAmount - x.CreditAmount), ct) ?? 0m;

    private async Task EnsureGroupAsync(long groupId, CancellationToken ct)
    {
        if (!await db.Groups.AnyAsync(x => x.Id == groupId && x.IsActive, ct))
            throw new ExpenseValidationException("The selected group does not exist or is inactive.");
        var configuredCount = await db.Groups.CountAsync(x => x.IsActive, ct);
        if (configuredCount != 1) throw new ExpenseValidationException("The system requires exactly one active configured group.");
    }

    private static string NormalizeRequired(string? value, string field, int maxLength)
    {
        var result = value?.Trim() ?? string.Empty;
        if (result.Length == 0) throw new ExpenseValidationException($"{field} is required.");
        if (result.Length > maxLength) throw new ExpenseValidationException($"{field} cannot exceed {maxLength} characters.");
        return result;
    }

    private static string NormalizePaymentMethod(string? value)
    {
        var result = value?.Trim() ?? string.Empty;
        if (!PaymentMethods.Contains(result, StringComparer.OrdinalIgnoreCase))
            throw new ExpenseValidationException("Payment method must be Cash, Bank, Online Transfer, or Other.");
        return PaymentMethods.First(x => x.Equals(result, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0m) throw new ExpenseValidationException("Expense amount must be greater than zero.");
        if (amount > 9999999999999999.99m) throw new ExpenseValidationException("Expense amount is too large.");
    }

    private static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static ExpenseCategoryResponse MapCategory(ExpenseCategory x) => new(x.Id, x.GroupId, x.Group.Code, x.Group.Name, x.Name, x.IsActive, x.CreatedAtUtc, x.CreatedByUserId, x.ModifiedAtUtc, x.ModifiedByUserId);

    private static ExpenseResponse MapExpense(Expense x)
    {
        var account = x.FinancialTransaction?.Details.FirstOrDefault(d => d.CreditAmount > 0)?.FinancialAccount;
        var accountId = account?.Id ?? x.FinancialTransaction?.Details.FirstOrDefault(d => d.CreditAmount > 0)?.FinancialAccountId ?? 0;
        return new(
            x.Id, x.ExpenseNumber, x.GroupId, x.Group.Code, x.Group.Name,
            x.ExpenseCategoryId, x.ExpenseCategory.Name, x.ExpenseDate, x.Amount, x.PaymentMethod,
            accountId, account?.AccountCode ?? string.Empty, account?.AccountName ?? string.Empty,
            x.ReferenceNumber, x.Description, x.Status, x.FinancialTransactionId,
            x.FinancialTransaction?.TransactionNumber, x.CreatedAtUtc, x.CreatedByUserId);
    }
}
