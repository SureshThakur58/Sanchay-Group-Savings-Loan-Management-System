using GroupFinance.Core.Entities;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Ledger;

public sealed class LedgerValidationException(string message) : Exception(message);

public sealed class LedgerService(GroupFinanceDbContext db)
{
    public async Task<IReadOnlyCollection<LedgerEntryResponse>> GetGroupLedgerAsync(
        long groupId,
        DateTime? fromDate,
        DateTime? toDate,
        long? financialAccountId,
        CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);
        var (from, to) = NormalizeRange(fromDate, toDate);

        var query = db.TransactionDetails
            .AsNoTracking()
            .Include(x => x.FinancialAccount)
            .Include(x => x.FinancialTransaction)
            .ThenInclude(x => x.Member)
            .Where(x => x.FinancialTransaction.GroupId == groupId &&
                        x.FinancialTransaction.Status == "Posted");

        if (from.HasValue) query = query.Where(x => x.FinancialTransaction.TransactionDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.FinancialTransaction.TransactionDate < to.Value.AddDays(1));
        if (financialAccountId.HasValue) query = query.Where(x => x.FinancialAccountId == financialAccountId.Value);

        var rows = await query
            .OrderBy(x => x.FinancialTransaction.TransactionDate)
            .ThenBy(x => x.FinancialTransaction.Id)
            .ThenBy(x => x.Id)
            .Take(2000)
            .ToListAsync(ct);

        var openingBalances = await GetOpeningBalancesAsync(
            groupId, from, financialAccountId, ct);

        return BuildEntries(rows, openingBalances);
    }

    public async Task<IReadOnlyCollection<LedgerEntryResponse>> GetMemberLedgerAsync(
        long groupId,
        long memberId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);
        if (!await db.Members.AnyAsync(x => x.Id == memberId && x.GroupId == groupId, ct))
            throw new LedgerValidationException("The selected member does not belong to the selected group.");

        var (from, to) = NormalizeRange(fromDate, toDate);

        var query = db.TransactionDetails
            .AsNoTracking()
            .Include(x => x.FinancialAccount)
            .Include(x => x.FinancialTransaction)
            .ThenInclude(x => x.Member)
            .Where(x => x.FinancialTransaction.GroupId == groupId &&
                        x.FinancialTransaction.MemberId == memberId &&
                        x.FinancialTransaction.Status == "Posted");

        if (from.HasValue) query = query.Where(x => x.FinancialTransaction.TransactionDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.FinancialTransaction.TransactionDate < to.Value.AddDays(1));

        var rows = await query
            .OrderBy(x => x.FinancialTransaction.TransactionDate)
            .ThenBy(x => x.FinancialTransaction.Id)
            .ThenBy(x => x.Id)
            .Take(2000)
            .ToListAsync(ct);

        var openingBalances = await GetMemberOpeningBalancesAsync(groupId, memberId, from, ct);
        return BuildEntries(rows, openingBalances);
    }

    public async Task<TrialBalanceResponse> GetTrialBalanceAsync(
        long groupId,
        DateTime asOfDate,
        CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);
        if (asOfDate == default)
            throw new LedgerValidationException("As-of date is required.");

        var endExclusive = asOfDate.Date.AddDays(1);
        var accounts = await db.FinancialAccounts
            .AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .OrderBy(x => x.AccountCode)
            .ToListAsync(ct);

        var totals = await db.TransactionDetails
            .AsNoTracking()
            .Where(x => x.FinancialTransaction.GroupId == groupId &&
                        x.FinancialTransaction.Status == "Posted" &&
                        x.FinancialTransaction.TransactionDate < endExclusive)
            .GroupBy(x => x.FinancialAccountId)
            .Select(g => new
            {
                FinancialAccountId = g.Key,
                TotalDebit = g.Sum(x => x.DebitAmount),
                TotalCredit = g.Sum(x => x.CreditAmount)
            })
            .ToDictionaryAsync(x => x.FinancialAccountId, ct);

        var rows = accounts.Select(account =>
        {
            var totalDebit = totals.GetValueOrDefault(account.Id)?.TotalDebit ?? 0m;
            var totalCredit = totals.GetValueOrDefault(account.Id)?.TotalCredit ?? 0m;
            var net = totalDebit - totalCredit;
            return new TrialBalanceRowResponse(
                account.Id,
                account.AccountCode,
                account.AccountName,
                account.AccountType,
                totalDebit,
                totalCredit,
                Math.Max(0m, net),
                Math.Max(0m, -net));
        }).ToArray();

        return new TrialBalanceResponse(
            groupId,
            asOfDate.Date,
            rows,
            rows.Sum(x => x.DebitBalance),
            rows.Sum(x => x.CreditBalance));
    }

    private async Task<Dictionary<long, decimal>> GetOpeningBalancesAsync(
        long groupId,
        DateTime? fromDate,
        long? financialAccountId,
        CancellationToken ct)
    {
        if (!fromDate.HasValue)
            return [];

        var query = db.TransactionDetails
            .AsNoTracking()
            .Where(x => x.FinancialTransaction.GroupId == groupId &&
                        x.FinancialTransaction.Status == "Posted" &&
                        x.FinancialTransaction.TransactionDate < fromDate.Value);
        if (financialAccountId.HasValue)
            query = query.Where(x => x.FinancialAccountId == financialAccountId.Value);

        return await query
            .GroupBy(x => x.FinancialAccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Balance = g.Sum(x => x.DebitAmount - x.CreditAmount)
            })
            .ToDictionaryAsync(x => x.AccountId, x => x.Balance, ct);
    }

    private async Task<Dictionary<long, decimal>> GetMemberOpeningBalancesAsync(
        long groupId,
        long memberId,
        DateTime? fromDate,
        CancellationToken ct)
    {
        if (!fromDate.HasValue)
            return [];

        return await db.TransactionDetails
            .AsNoTracking()
            .Where(x => x.FinancialTransaction.GroupId == groupId &&
                        x.FinancialTransaction.MemberId == memberId &&
                        x.FinancialTransaction.Status == "Posted" &&
                        x.FinancialTransaction.TransactionDate < fromDate.Value)
            .GroupBy(x => x.FinancialAccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Balance = g.Sum(x => x.DebitAmount - x.CreditAmount)
            })
            .ToDictionaryAsync(x => x.AccountId, x => x.Balance, ct);
    }

    private static IReadOnlyCollection<LedgerEntryResponse> BuildEntries(
        IReadOnlyList<TransactionDetail> rows,
        IReadOnlyDictionary<long, decimal> openingBalances)
    {
        var running = openingBalances.ToDictionary(x => x.Key, x => x.Value);
        var result = new List<LedgerEntryResponse>(rows.Count);

        foreach (var row in rows)
        {
            var transaction = row.FinancialTransaction;
            var balance = running.GetValueOrDefault(row.FinancialAccountId);
            balance += row.DebitAmount - row.CreditAmount;
            running[row.FinancialAccountId] = balance;

            result.Add(new LedgerEntryResponse(
                row.Id,
                transaction.Id,
                transaction.TransactionNumber,
                transaction.TransactionDate,
                transaction.TransactionType,
                transaction.Description,
                row.FinancialAccountId,
                row.FinancialAccount.AccountCode,
                row.FinancialAccount.AccountName,
                row.DebitAmount,
                row.CreditAmount,
                balance,
                transaction.MemberId,
                transaction.Member?.MemberNumber,
                transaction.Member?.FullName,
                transaction.ReferenceNumber));
        }

        return result;
    }

    private async Task EnsureGroupAsync(long groupId, CancellationToken ct)
    {
        if (groupId <= 0)
            throw new LedgerValidationException("groupId is required.");
        if (!await db.Groups.AnyAsync(x => x.Id == groupId && x.IsActive, ct))
            throw new LedgerValidationException("The selected group does not exist or is inactive.");
    }

    private static (DateTime? From, DateTime? To) NormalizeRange(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate?.Date;
        var to = toDate?.Date;
        if (from.HasValue && to.HasValue && to.Value < from.Value)
            throw new LedgerValidationException("To date cannot be earlier than from date.");
        return (from, to);
    }
}
