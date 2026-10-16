using System.Globalization;
using System.Security.Claims;
using GroupFinance.Core.Entities;
using GroupFinance.Core.Enums;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.FinancialTransactions;

public sealed class FinancialTransactionService(GroupFinanceDbContext db)
{
    private const string NumberFormatSettingKey = "FinancialTransactionNumberFormat";
    private const string DefaultNumberFormat = "FT-{0:00000000}";

    public async Task<FinancialTransactionResponse> CreateAsync(
        CreateFinancialTransactionRequest request,
        long userId,
        CancellationToken cancellationToken)
    {
        var executionStrategy = db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await CreatePostedInCurrentTransactionAsync(request, userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    /// <summary>
    /// Creates a posted financial transaction using the caller's existing database transaction.
    /// Contribution Engine uses this to atomically persist the contribution payment and its journal entry.
    /// </summary>
    public async Task<FinancialTransactionResponse> CreatePostedInCurrentTransactionAsync(
        CreateFinancialTransactionRequest request,
        long userId,
        CancellationToken cancellationToken)
    {
        ValidateHeader(request);

        var group = await db.Groups
            .Include(x => x.Setting)
            .SingleOrDefaultAsync(x => x.Id == request.GroupId && x.IsActive, cancellationToken)
            ?? throw new FinancialTransactionValidationException("The selected group does not exist or is inactive.");

        if (request.MemberId.HasValue)
        {
            var memberBelongsToGroup = await db.Members.AnyAsync(
                x => x.Id == request.MemberId.Value && x.GroupId == request.GroupId,
                cancellationToken);
            if (!memberBelongsToGroup)
                throw new FinancialTransactionValidationException("The selected member does not belong to the selected group.");
        }

        var transactionDate = request.TransactionDate.Kind == DateTimeKind.Unspecified
            ? request.TransactionDate
            : request.TransactionDate.ToUniversalTime();

        var todayUtc = DateTime.UtcNow.Date;
        if (transactionDate.Date < todayUtc && group.Setting?.AllowBackdatedTransactions == false)
            throw new FinancialTransactionValidationException("Backdated financial transactions are disabled for this group.");
        if (transactionDate.Date > todayUtc && group.Setting?.AllowFutureFinancialPosting != true)
            throw new FinancialTransactionValidationException("Future financial posting is disabled for this group.");

        var accountIds = request.Lines.Select(x => x.FinancialAccountId).Distinct().ToArray();
        var accounts = await db.FinancialAccounts
            .Where(x => x.GroupId == request.GroupId && accountIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (accounts.Count != accountIds.Length)
            throw new FinancialTransactionValidationException("Every financial account must exist, be active, and belong to the selected group.");
        if (accounts.Values.Any(x => !x.IsActive))
            throw new FinancialTransactionValidationException("Inactive financial accounts cannot receive new transactions.");

        var totalDebit = request.Lines.Sum(x => decimal.Round(x.DebitAmount, 2, MidpointRounding.AwayFromZero));
        var totalCredit = request.Lines.Sum(x => decimal.Round(x.CreditAmount, 2, MidpointRounding.AwayFromZero));

        if (totalDebit <= 0m || totalCredit <= 0m)
            throw new FinancialTransactionValidationException("A financial transaction must contain positive debit and credit totals.");
        if (totalDebit != totalCredit)
            throw new FinancialTransactionValidationException("The transaction is not balanced. Total debit must equal total credit.");

        var entity = new FinancialTransaction
        {
            TransactionNumber = $"PENDING-{Guid.NewGuid():N}",
            TransactionDate = transactionDate,
            GroupId = request.GroupId,
            MemberId = request.MemberId,
            TransactionType = request.TransactionType.Trim().ToUpperInvariant(),
            Status = "Posted",
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ReferenceType = string.IsNullOrWhiteSpace(request.ReferenceType) ? null : request.ReferenceType.Trim(),
            ReferenceId = request.ReferenceId,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
            Amount = totalDebit,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        foreach (var line in request.Lines)
        {
            var debit = decimal.Round(line.DebitAmount, 2, MidpointRounding.AwayFromZero);
            var credit = decimal.Round(line.CreditAmount, 2, MidpointRounding.AwayFromZero);

            if ((debit > 0m && credit != 0m) || (credit > 0m && debit != 0m) || (debit == 0m && credit == 0m))
                throw new FinancialTransactionValidationException("Each transaction line must contain either a debit or a credit amount, but never both.");

            entity.Details.Add(new TransactionDetail
            {
                FinancialAccountId = line.FinancialAccountId,
                DebitAmount = debit,
                CreditAmount = credit,
                Description = string.IsNullOrWhiteSpace(line.Description) ? null : line.Description.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            });
        }

        db.FinancialTransactions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        var numberFormat = await GetNumberFormatAsync(cancellationToken);
        entity.TransactionNumber = FormatTransactionNumber(numberFormat, entity.Id);
        await db.SaveChangesAsync(cancellationToken);

        return Map(entity, accounts);
    }

    public async Task<IReadOnlyCollection<FinancialTransactionResponse>> ListAsync(
        long groupId,
        DateTime? fromDate,
        DateTime? toDate,
        string? transactionType,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = db.FinancialTransactions
            .AsNoTracking()
            .Include(x => x.Details)
            .ThenInclude(x => x.FinancialAccount)
            .Where(x => x.GroupId == groupId);

        if (fromDate.HasValue) query = query.Where(x => x.TransactionDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.TransactionDate < toDate.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(transactionType)) query = query.Where(x => x.TransactionType == transactionType.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());

        var entities = await query
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        return entities.Select(x => Map(
            x,
            x.Details
                .GroupBy(d => d.FinancialAccountId)
                .ToDictionary(g => g.Key, g => g.First().FinancialAccount)))
            .ToArray();
    }

    public async Task<FinancialTransactionResponse?> GetAsync(long id, CancellationToken cancellationToken)
    {
        var entity = await db.FinancialTransactions
            .AsNoTracking()
            .Include(x => x.Details)
            .ThenInclude(x => x.FinancialAccount)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null
            ? null
            : Map(entity, entity.Details.GroupBy(d => d.FinancialAccountId).ToDictionary(g => g.Key, g => g.First().FinancialAccount));
    }

    public async Task<IReadOnlyCollection<FinancialAccountResponse>> GetAccountsAsync(long groupId, CancellationToken cancellationToken)
    {
        return await db.FinancialAccounts
            .AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .OrderBy(x => x.AccountCode)
            .Select(x => new FinancialAccountResponse(x.Id, x.GroupId, x.AccountCode, x.AccountName, x.AccountType, x.IsSystemAccount, x.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<FinancialAccountResponse> CreateAccountAsync(CreateFinancialAccountRequest request, long userId, CancellationToken cancellationToken)
    {
        if (request.GroupId <= 0) throw new FinancialTransactionValidationException("GroupId is required.");
        if (string.IsNullOrWhiteSpace(request.AccountCode)) throw new FinancialTransactionValidationException("Account code is required.");
        if (string.IsNullOrWhiteSpace(request.AccountName)) throw new FinancialTransactionValidationException("Account name is required.");
        if (string.IsNullOrWhiteSpace(request.AccountType)) throw new FinancialTransactionValidationException("Account type is required.");
        var accountType = request.AccountType.Trim();
        if (!string.Equals(accountType, "Cash", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(accountType, "Bank", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(accountType, "Other", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(accountType, "Asset", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(accountType, "Liability", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(accountType, "Equity", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(accountType, "Income", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(accountType, "Expense", StringComparison.OrdinalIgnoreCase))
            throw new FinancialTransactionValidationException("Unsupported account type.");

        if (!await db.Groups.AnyAsync(x => x.Id == request.GroupId && x.IsActive, cancellationToken))
            throw new FinancialTransactionValidationException("The selected group does not exist or is inactive.");

        var code = request.AccountCode.Trim();
        if (await db.FinancialAccounts.AnyAsync(x => x.GroupId == request.GroupId && x.AccountCode == code, cancellationToken))
            throw new FinancialTransactionValidationException("An account with this code already exists in the selected group.");

        var entity = new FinancialAccount
        {
            GroupId = request.GroupId,
            AccountCode = code,
            AccountName = request.AccountName.Trim(),
            AccountType = accountType switch
            {
                var t when string.Equals(t, "Cash", StringComparison.OrdinalIgnoreCase) => "Cash",
                var t when string.Equals(t, "Bank", StringComparison.OrdinalIgnoreCase) => "Bank",
                var t when string.Equals(t, "Other", StringComparison.OrdinalIgnoreCase) => "Other",
                var t when string.Equals(t, "Asset", StringComparison.OrdinalIgnoreCase) => "Asset",
                var t when string.Equals(t, "Liability", StringComparison.OrdinalIgnoreCase) => "Liability",
                var t when string.Equals(t, "Equity", StringComparison.OrdinalIgnoreCase) => "Equity",
                var t when string.Equals(t, "Income", StringComparison.OrdinalIgnoreCase) => "Income",
                _ => "Expense"
            },
            IsSystemAccount = request.IsSystemAccount,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        db.FinancialAccounts.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return new FinancialAccountResponse(entity.Id, entity.GroupId, entity.AccountCode, entity.AccountName, entity.AccountType, entity.IsSystemAccount, entity.IsActive);
    }

    public async Task<FinancialAccountBalanceResponse?> GetAccountBalanceAsync(long accountId, CancellationToken cancellationToken)
    {
        var account = await db.FinancialAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId, cancellationToken);
        if (account is null) return null;

        var totals = await db.TransactionDetails
            .Where(x => x.FinancialAccountId == accountId && x.FinancialTransaction.Status == "Posted")
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .SingleOrDefaultAsync(cancellationToken);

        var debit = totals?.Debit ?? 0m;
        var credit = totals?.Credit ?? 0m;
        return new FinancialAccountBalanceResponse(account.Id, account.AccountCode, account.AccountName, debit, credit, debit - credit);
    }

    private async Task<string> GetNumberFormatAsync(CancellationToken cancellationToken)
    {
        var setting = await db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(x => x.SettingKey == NumberFormatSettingKey, cancellationToken);
        return string.IsNullOrWhiteSpace(setting?.SettingValue) ? DefaultNumberFormat : setting.SettingValue.Trim();
    }

    private static string FormatTransactionNumber(string format, long id)
    {
        try
        {
            var result = string.Format(CultureInfo.InvariantCulture, format, id);
            if (string.IsNullOrWhiteSpace(result) || result.Length > 40)
                throw new FormatException();
            return result;
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
        {
            throw new FinancialTransactionValidationException("FinancialTransactionNumberFormat is invalid. Use a format containing {0}, for example FT-{0:00000000}.");
        }
    }

    private static void ValidateHeader(CreateFinancialTransactionRequest request)
    {
        if (request.GroupId <= 0) throw new FinancialTransactionValidationException("GroupId is required.");
        if (request.TransactionDate == default) throw new FinancialTransactionValidationException("Transaction date is required.");
        if (!FinancialTransactionTypes.All.Contains(request.TransactionType?.Trim().ToUpperInvariant(), StringComparer.Ordinal))
            throw new FinancialTransactionValidationException("Unsupported financial transaction type.");
        if (request.Lines is null || request.Lines.Count < 2)
            throw new FinancialTransactionValidationException("A double-entry transaction requires at least two lines.");
        if (request.Lines.Count > 100)
            throw new FinancialTransactionValidationException("A transaction cannot contain more than 100 lines.");
    }

    private static FinancialTransactionResponse Map(FinancialTransaction entity, IReadOnlyDictionary<long, FinancialAccount> accounts)
    {
        var lines = entity.Details.Select(d => new FinancialTransactionLineResponse(
            d.Id,
            d.FinancialAccountId,
            accounts[d.FinancialAccountId].AccountCode,
            accounts[d.FinancialAccountId].AccountName,
            d.DebitAmount,
            d.CreditAmount,
            d.Description)).ToArray();

        return new FinancialTransactionResponse(
            entity.Id,
            entity.TransactionNumber,
            entity.TransactionDate,
            entity.GroupId,
            entity.MemberId,
            entity.TransactionType,
            entity.Status,
            entity.Description,
            entity.ReferenceType,
            entity.ReferenceId,
            entity.ReferenceNumber,
            entity.Amount,
            entity.TotalDebit,
            entity.TotalCredit,
            entity.CreatedByUserId,
            entity.CreatedAtUtc,
            lines);
    }
}

public sealed class FinancialTransactionValidationException(string message) : Exception(message);
