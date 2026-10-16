using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Entities;
using GroupFinance.Core.Enums;
using GroupFinance.Core.Constants;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.GroupFund;

public sealed class GroupFundService(GroupFinanceDbContext db, FinancialTransactionService financialTransactions)
{
    private static readonly string[] FundAccountTypes = ["Cash", "Bank", "Other"];

    public async Task<IReadOnlyCollection<GroupFundAccountResponse>> GetAccountsAsync(long groupId, CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);
        var accounts = await db.FinancialAccounts.AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .OrderBy(x => x.AccountType).ThenBy(x => x.AccountCode)
            .ToListAsync(ct);

        var ids = accounts.Select(x => x.Id).ToArray();
        var totals = await db.TransactionDetails.AsNoTracking()
            .Where(x => ids.Contains(x.FinancialAccountId) && x.FinancialTransaction.Status == "Posted")
            .GroupBy(x => x.FinancialAccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .ToDictionaryAsync(x => x.AccountId, ct);

        return accounts.Select(a =>
        {
            totals.TryGetValue(a.Id, out var t);
            var debit = t?.Debit ?? 0m;
            var credit = t?.Credit ?? 0m;
            return new GroupFundAccountResponse(a.Id, a.GroupId, a.AccountCode, a.AccountName, a.AccountType, a.IsSystemAccount, a.IsActive, debit, credit, debit - credit);
        }).ToArray();
    }

    public async Task<GroupFundAccountResponse> UpdateAccountAsync(long id, UpdateGroupFundAccountRequest request, long userId, CancellationToken ct)
    {
        var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new GroupFundValidationException("Financial account not found.");
        if (account.IsSystemAccount)
            throw new GroupFundValidationException("System financial accounts cannot be modified from Group Fund management.");
        if (string.IsNullOrWhiteSpace(request.AccountName))
            throw new GroupFundValidationException("Account name is required.");
        var type = NormalizeAccountType(request.AccountType);
        if (!FundAccountTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            throw new GroupFundValidationException("Account type must be Cash, Bank, or Other.");
        var hasActivity = await HasPostedActivityAsync(id, ct);
        if (!request.IsActive && hasActivity)
            throw new GroupFundValidationException("A financial account with posted transactions cannot be deactivated.");
        if (hasActivity && !string.Equals(account.AccountType, type, StringComparison.OrdinalIgnoreCase))
            throw new GroupFundValidationException("A financial account with posted transactions cannot change account type.");

        account.AccountName = request.AccountName.Trim();
        account.AccountType = type;
        account.IsActive = request.IsActive;
        account.ModifiedAtUtc = DateTime.UtcNow;
        account.ModifiedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return await GetAccountAsync(account, ct);
    }

    public async Task<OpeningFundResponse> CreateOpeningFundAsync(CreateOpeningFundRequest request, long userId, CancellationToken ct)
    {
        ValidateAmount(request.Amount);
        await EnsureGroupAsync(request.GroupId, ct);
        ValidateTransactionDate(request.TransactionDate);

        var executionStrategy = db.Database.CreateExecutionStrategy();
        var transaction = await executionStrategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            var account = await GetActiveFundAccountAsync(request.GroupId, request.FinancialAccountId, ct);
            if (await db.FinancialTransactions.AnyAsync(x => x.GroupId == request.GroupId && x.Status == "Posted" && x.ReferenceType == "OPENING_FUND", ct))
                throw new GroupFundValidationException("An opening fund has already been recorded for this group.");

            var equity = await EnsureOpeningFundAccountAsync(request.GroupId, userId, ct);
            var posted = await financialTransactions.CreatePostedInCurrentTransactionAsync(
                new CreateFinancialTransactionRequest(
                    request.GroupId, null, request.TransactionDate, FinancialTransactionTypes.Adjustment,
                    string.IsNullOrWhiteSpace(request.Description) ? "Opening group fund" : request.Description,
                    "OPENING_FUND", null, request.ReferenceNumber,
                    [
                        new FinancialTransactionLineRequest(account.Id, request.Amount, 0m, "Opening group fund"),
                        new FinancialTransactionLineRequest(equity.Id, 0m, request.Amount, "Opening fund equity")
                    ]), userId, ct);
            await dbTransaction.CommitAsync(ct);
            return posted;
        });

        return new OpeningFundResponse(transaction.Id, transaction.TransactionNumber, transaction.TransactionDate, request.FinancialAccountId, request.Amount, transaction.Status, transaction.ReferenceNumber, transaction.Description);
    }

    public async Task<FundTransferResponse> TransferAsync(FundTransferRequest request, long userId, CancellationToken ct)
    {
        ValidateAmount(request.Amount);
        await EnsureGroupAsync(request.GroupId, ct);
        ValidateTransactionDate(request.TransactionDate);
        if (request.FromFinancialAccountId == request.ToFinancialAccountId)
            throw new GroupFundValidationException("From and To financial accounts must be different.");

        var executionStrategy = db.Database.CreateExecutionStrategy();
        var transaction = await executionStrategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            var from = await GetActiveFundAccountAsync(request.GroupId, request.FromFinancialAccountId, ct);
            var to = await GetActiveFundAccountAsync(request.GroupId, request.ToFinancialAccountId, ct);
            var balance = await GetAccountBalanceValueAsync(from.Id, ct);
            if (balance < request.Amount)
                throw new GroupFundValidationException("Insufficient available balance in the source financial account.");

            var posted = await financialTransactions.CreatePostedInCurrentTransactionAsync(
                new CreateFinancialTransactionRequest(
                    request.GroupId, null, request.TransactionDate, FinancialTransactionTypes.Transfer,
                    string.IsNullOrWhiteSpace(request.Description) ? "Fund transfer" : request.Description,
                    "FUND_TRANSFER", null, request.ReferenceNumber,
                    [
                        new FinancialTransactionLineRequest(to.Id, request.Amount, 0m, "Fund transfer in"),
                        new FinancialTransactionLineRequest(from.Id, 0m, request.Amount, "Fund transfer out")
                    ]), userId, ct);
            await dbTransaction.CommitAsync(ct);
            return posted;
        });

        return new FundTransferResponse(transaction.Id, transaction.TransactionNumber, transaction.TransactionDate, request.FromFinancialAccountId, request.ToFinancialAccountId, request.Amount, transaction.Status, transaction.ReferenceNumber, transaction.Description);
    }

    public async Task<GroupFundSummaryResponse> GetSummaryAsync(long groupId, DateTime? fromDate, DateTime? toDate, CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);
        var firstOpeningFundDate = await db.FinancialTransactions.AsNoTracking()
            .Where(x => x.GroupId == groupId && x.Status == "Posted" && x.ReferenceType == "OPENING_FUND")
            .Select(x => (DateTime?)x.TransactionDate)
            .MinAsync(ct);
        var firstTransactionDate = await db.FinancialTransactions.AsNoTracking()
            .Where(x => x.GroupId == groupId && x.Status == "Posted")
            .Select(x => (DateTime?)x.TransactionDate)
            .MinAsync(ct);
        var start = (fromDate ?? firstOpeningFundDate ?? firstTransactionDate ?? DateTime.UtcNow).Date;
        var end = (toDate ?? DateTime.UtcNow).Date;
        if (end < start) throw new GroupFundValidationException("toDate cannot be earlier than fromDate.");

        var fundAccountIds = await db.FinancialAccounts.AsNoTracking()
            .Where(x => x.GroupId == groupId && FundAccountTypes.Contains(x.AccountType))
            .Select(x => x.Id).ToArrayAsync(ct);

        var openingFund = await GetFundBalanceBeforeAsync(groupId, start, fundAccountIds, ct);
        if (fundAccountIds.Length > 0)
        {
            var openingFundAtStart = await db.FinancialTransactions.AsNoTracking()
                .Where(x => x.GroupId == groupId && x.Status == "Posted" && x.ReferenceType == "OPENING_FUND" && x.TransactionDate >= start && x.TransactionDate < start.AddDays(1))
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            openingFund += openingFundAtStart;
        }
        var periodTransactions = db.FinancialTransactions.AsNoTracking()
            .Where(x => x.GroupId == groupId && x.Status == "Posted" && x.TransactionDate >= start && x.TransactionDate < end.AddDays(1));

        var contributions = await periodTransactions.Where(x => x.TransactionType == FinancialTransactionTypes.Contribution).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var loanInterest = await periodTransactions.Where(x => x.TransactionType == FinancialTransactionTypes.InterestIncome).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var loansGiven = await periodTransactions.Where(x => x.TransactionType == FinancialTransactionTypes.LoanDisbursement).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var expenses = await periodTransactions.Where(x => x.TransactionType == FinancialTransactionTypes.Expense).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var assetPurchases = await periodTransactions.Where(x => x.TransactionType == FinancialTransactionTypes.AssetPurchase).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

        // The current transaction taxonomy has no separate OTHER_INCOME, INVESTMENT_INCOME,
        // or WITHDRAWAL type. These remain zero until a later phase introduces those postings.
        const decimal otherIncome = 0m;
        const decimal investmentIncome = 0m;
        const decimal withdrawals = 0m;

        var totalAvailable = GroupFundCalculation.TotalAvailableFund(openingFund, contributions, loanInterest, otherIncome, investmentIncome);
        var closingFund = GroupFundCalculation.ClosingFund(totalAvailable, loansGiven, expenses, assetPurchases, withdrawals);

        var cashBalance = await GetFundTypeBalanceAsync(groupId, "Cash", end, ct);
        var bankBalance = await GetFundTypeBalanceAsync(groupId, "Bank", end, ct);
        var cashBankBalance = cashBalance + bankBalance;
        return new GroupFundSummaryResponse(groupId, start, end, openingFund, contributions, loanInterest, otherIncome, investmentIncome, totalAvailable, loansGiven, expenses, assetPurchases, withdrawals, closingFund, cashBankBalance, cashBalance, bankBalance);
    }

    private async Task<decimal> GetFundBalanceBeforeAsync(long groupId, DateTime start, long[] accountIds, CancellationToken ct)
    {
        if (accountIds.Length == 0) return 0m;
        return await db.TransactionDetails.AsNoTracking()
            .Where(x => accountIds.Contains(x.FinancialAccountId) && x.FinancialTransaction.GroupId == groupId && x.FinancialTransaction.Status == "Posted" && x.FinancialTransaction.TransactionDate < start)
            .SumAsync(x => (decimal?)(x.DebitAmount - x.CreditAmount), ct) ?? 0m;
    }

    private async Task<decimal> GetFundTypeBalanceAsync(long groupId, string accountType, DateTime asOf, CancellationToken ct)
    {
        return await db.TransactionDetails.AsNoTracking()
            .Where(x => x.FinancialAccount.GroupId == groupId && x.FinancialAccount.AccountType == accountType && x.FinancialTransaction.Status == "Posted" && x.FinancialTransaction.TransactionDate < asOf.AddDays(1))
            .SumAsync(x => (decimal?)(x.DebitAmount - x.CreditAmount), ct) ?? 0m;
    }

    private async Task<FinancialAccount> GetActiveFundAccountAsync(long groupId, long accountId, CancellationToken ct)
    {
        var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.Id == accountId && x.GroupId == groupId && x.IsActive, ct)
            ?? throw new GroupFundValidationException("The selected financial account does not exist, is inactive, or belongs to another group.");
        if (!FundAccountTypes.Contains(account.AccountType, StringComparer.OrdinalIgnoreCase))
            throw new GroupFundValidationException("The selected account must be a Cash, Bank, or Other fund account.");
        return account;
    }

    private async Task<FinancialAccount> EnsureOpeningFundAccountAsync(long groupId, long userId, CancellationToken ct)
    {
        var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.GroupId == groupId && x.AccountCode == "OPENING-FUND", ct);
        if (account is not null) return account;
        account = new FinancialAccount
        {
            GroupId = groupId,
            AccountCode = "OPENING-FUND",
            AccountName = "Opening Fund Equity",
            AccountType = "Equity",
            IsSystemAccount = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        db.FinancialAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }

    private async Task<GroupFundAccountResponse> GetAccountAsync(FinancialAccount account, CancellationToken ct)
    {
        var totals = await db.TransactionDetails.AsNoTracking()
            .Where(x => x.FinancialAccountId == account.Id && x.FinancialTransaction.Status == "Posted")
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .SingleOrDefaultAsync(ct);
        var debit = totals?.Debit ?? 0m;
        var credit = totals?.Credit ?? 0m;
        return new GroupFundAccountResponse(account.Id, account.GroupId, account.AccountCode, account.AccountName, account.AccountType, account.IsSystemAccount, account.IsActive, debit, credit, debit - credit);
    }

    private async Task<bool> HasPostedActivityAsync(long accountId, CancellationToken ct) =>
        await db.TransactionDetails.AnyAsync(x => x.FinancialAccountId == accountId && x.FinancialTransaction.Status == "Posted", ct);

    private async Task<decimal> GetAccountBalanceValueAsync(long accountId, CancellationToken ct) =>
        await db.TransactionDetails.AsNoTracking()
            .Where(x => x.FinancialAccountId == accountId && x.FinancialTransaction.Status == "Posted")
            .SumAsync(x => (decimal?)(x.DebitAmount - x.CreditAmount), ct) ?? 0m;

    private async Task EnsureGroupAsync(long groupId, CancellationToken ct)
    {
        if (groupId <= 0 || !await db.Groups.AnyAsync(x => x.Id == groupId && x.IsActive, ct))
            throw new GroupFundValidationException("The selected group does not exist or is inactive.");
    }

    private static string NormalizeAccountType(string value)
    {
        var normalized = value?.Trim();
        if (string.Equals(normalized, "Cash", StringComparison.OrdinalIgnoreCase)) return "Cash";
        if (string.Equals(normalized, "Bank", StringComparison.OrdinalIgnoreCase)) return "Bank";
        if (string.Equals(normalized, "Other", StringComparison.OrdinalIgnoreCase)) return "Other";
        return normalized ?? string.Empty;
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0m) throw new GroupFundValidationException("Amount must be greater than zero.");
        if (decimal.Round(amount, 2) != amount) throw new GroupFundValidationException("Amount cannot have more than two decimal places.");
    }

    private static void ValidateTransactionDate(DateTime date)
    {
        if (date == default) throw new GroupFundValidationException("Transaction date is required.");
    }
}

public sealed class GroupFundValidationException(string message) : Exception(message);
