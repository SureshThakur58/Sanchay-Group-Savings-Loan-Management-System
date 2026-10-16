using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Api.InterestProfit;
using GroupFinance.Core.Entities;
using GroupFinance.Core.Enums;
using GroupFinance.Core.InterestProfit;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.ProfitDistributions;

public sealed class ProfitDistributionValidationException(string message) : Exception(message);

public sealed class ProfitDistributionService(
    GroupFinanceDbContext db,
    InterestProfitService interestProfitService,
    FinancialTransactionService financialTransactions)
{
    public async Task<ProfitDistributionResponse> PreviewAsync(
        ProfitDistributionPreviewRequest request,
        CancellationToken ct)
    {
        ValidateRequest(request.GroupId, request.PeriodStart, request.PeriodEnd, request.DistributionMethod);
        var group = await db.Groups.Include(x => x.Setting)
            .SingleOrDefaultAsync(x => x.Id == request.GroupId && x.IsActive, ct)
            ?? throw new ProfitDistributionValidationException("The selected group does not exist or is inactive.");
        var profit = await interestProfitService.CalculateProfitAsync(
            new ProfitCalculationRequest(request.GroupId, request.PeriodStart.Date, request.PeriodEnd.Date), ct);
        var members = await GetEligibleMembersAsync(request.GroupId, request.PeriodEnd.Date, request.PeriodStart.Date, request.PeriodEnd.Date, ct);
        var shares = CalculateShares(profit, members, request.DistributionMethod, group.Setting?.MonetaryPrecision ?? 2);
        return BuildPreviewResponse(request, profit, members, shares);
    }

    public async Task<ProfitDistributionResponse> CreateAsync(
        ProfitDistributionRequest request,
        long userId,
        CancellationToken ct)
    {
        ValidateRequest(request.GroupId, request.PeriodStart, request.PeriodEnd, request.DistributionMethod);
        var executionStrategy = db.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var start = request.PeriodStart.Date;
            var end = request.PeriodEnd.Date;
            var group = await db.Groups.Include(x => x.Setting)
                .SingleOrDefaultAsync(x => x.Id == request.GroupId && x.IsActive, ct)
                ?? throw new ProfitDistributionValidationException("The selected group does not exist or is inactive.");
            if (await db.Groups.CountAsync(ct) != 1)
                throw new ProfitDistributionValidationException("Exactly one group must be configured before profit distribution can be managed.");

            var existing = await db.ProfitDistributions
                .SingleOrDefaultAsync(x => x.GroupId == request.GroupId && x.PeriodStart == start && x.PeriodEnd == end, ct);
            if (existing is not null)
                throw new ProfitDistributionValidationException("A profit distribution already exists for the selected group and period.");

            var profit = await interestProfitService.CalculateProfitAsync(
                new ProfitCalculationRequest(request.GroupId, start, end), ct);
            if (profit.NetProfit <= 0m || profit.DistributableAmount <= 0m)
                throw new ProfitDistributionValidationException("There is no positive distributable profit for the selected period.");

            var members = await GetEligibleMembersAsync(request.GroupId, end, start, end, ct);
            var shares = CalculateShares(profit, members, request.DistributionMethod, profit.NetProfit == 0m ? 2 : group.Setting?.MonetaryPrecision ?? 2);
            var now = DateTime.UtcNow;

            var distribution = new ProfitDistribution
            {
                GroupId = request.GroupId,
                PeriodStart = start,
                PeriodEnd = end,
                NetProfit = profit.NetProfit,
                ReserveAmount = profit.ReserveAmount,
                DistributableAmount = profit.DistributableAmount,
                DistributionMethod = request.DistributionMethod.Trim(),
                Status = "Draft",
                CreatedAtUtc = now,
                CreatedByUserId = userId
            };
            db.ProfitDistributions.Add(distribution);
            await db.SaveChangesAsync(ct);

            foreach (var share in shares)
            {
                var member = members.Single(x => x.Member.Id == share.MemberId).Member;
                db.ProfitDistributionDetails.Add(new ProfitDistributionDetail
                {
                    ProfitDistributionId = distribution.Id,
                    MemberId = member.Id,
                    EligibilityFactor = share.EligibilityFactor,
                    CalculatedShare = share.CalculatedShare,
                    DistributedAmount = share.DistributedAmount,
                    CreatedAtUtc = now,
                    CreatedByUserId = userId
                });
            }
            await db.SaveChangesAsync(ct);

            var distributionAccount = await EnsureSystemAccountAsync(
                request.GroupId, "PROFIT-DISTRIBUTION", "Profit Distribution", "Equity", userId, ct);
            var lines = new List<FinancialTransactionLineRequest>
            {
                new(distributionAccount.Id, profit.DistributableAmount, 0m, $"Profit distribution {start:yyyy-MM-dd} to {end:yyyy-MM-dd}")
            };
            foreach (var share in shares.Where(x => x.DistributedAmount > 0m))
            {
                var payable = await EnsureSystemAccountAsync(
                    request.GroupId,
                    $"MEMBER-PROFIT-{share.MemberId}",
                    $"Member Profit Payable - {members.Single(x => x.Member.Id == share.MemberId).Member.MemberNumber}",
                    "Liability", userId, ct);
                lines.Add(new(payable.Id, 0m, share.DistributedAmount, $"Profit payable to member {members.Single(x => x.Member.Id == share.MemberId).Member.MemberNumber}"));
            }

            var journal = await financialTransactions.CreatePostedInCurrentTransactionAsync(
                new CreateFinancialTransactionRequest(
                    request.GroupId,
                    null,
                    end,
                    FinancialTransactionTypes.InterestDistribution,
                    string.IsNullOrWhiteSpace(request.Description)
                        ? $"Profit distribution for {start:dd/MM/yyyy} - {end:dd/MM/yyyy}"
                        : request.Description.Trim(),
                    "ProfitDistribution",
                    distribution.Id,
                    request.ReferenceNumber,
                    lines), userId, ct);

            distribution.Status = "Distributed";
            distribution.DistributedDate = end;
            distribution.ModifiedAtUtc = now;
            distribution.ModifiedByUserId = userId;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return await GetAsync(distribution.Id, ct, journal.Id)
                ?? throw new InvalidOperationException("The profit distribution was created but could not be reloaded.");
        });
    }

    public async Task<IReadOnlyCollection<ProfitDistributionResponse>> ListAsync(
        long groupId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        await EnsureGroupAsync(groupId, ct);
        var query = db.ProfitDistributions.AsNoTracking()
            .Include(x => x.Details).ThenInclude(x => x.Member)
            .Where(x => x.GroupId == groupId);
        if (fromDate.HasValue) query = query.Where(x => x.PeriodEnd >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.PeriodStart <= toDate.Value.Date);
        var rows = await query.OrderByDescending(x => x.PeriodStart).Take(500).ToListAsync(ct);
        return rows.Select(x => Map(x, null)).ToArray();
    }

    public async Task<ProfitDistributionResponse?> GetAsync(long id, CancellationToken ct, long? transactionIdOverride = null)
    {
        var row = await db.ProfitDistributions.AsNoTracking()
            .Include(x => x.Details).ThenInclude(x => x.Member)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : Map(row, transactionIdOverride ?? await FindTransactionIdAsync(id, ct));
    }

    private async Task<IReadOnlyList<EligibleMember>> GetEligibleMembersAsync(long groupId, DateTime asOfDate, DateTime start, DateTime end, CancellationToken ct)
    {
        var members = await db.Members.AsNoTracking()
            .Where(x => x.GroupId == groupId && x.IsActive && x.JoinDate.Date <= asOfDate && (x.ExitDate == null || x.ExitDate.Value.Date > asOfDate))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        if (members.Count == 0)
            throw new ProfitDistributionValidationException("No active members are eligible for profit distribution for the selected period.");

        var memberIds = members.Select(x => x.Id).ToArray();
        var contributions = await db.ContributionPayments.AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.Status == "Posted" && x.TransactionDate.Date <= end)
            .GroupBy(x => x.MemberId)
            .Select(g => new { MemberId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.MemberId, x => x.Total, ct);
        var periodContributions = await db.ContributionPayments.AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.Status == "Posted" && x.TransactionDate.Date >= start && x.TransactionDate.Date <= end)
            .GroupBy(x => x.MemberId)
            .Select(g => new { MemberId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.MemberId, x => x.Total, ct);

        return members.Select(member =>
        {
            var openingBalance = contributions.GetValueOrDefault(member.Id) - periodContributions.GetValueOrDefault(member.Id);
            var closingBalance = contributions.GetValueOrDefault(member.Id);
            var averageBalance = (Math.Max(0m, openingBalance) + Math.Max(0m, closingBalance)) / 2m;
            var membershipMonths = CompletedMonths(member.JoinDate.Date, end);
            return new EligibleMember(member, Math.Max(0m, closingBalance), membershipMonths, averageBalance);
        }).ToArray();
    }

    private static IReadOnlyList<ProfitDistributionShare> CalculateShares(ProfitCalculationResponse profit, IReadOnlyList<EligibleMember> members, string method, int precision)
    {
        try
        {
            return ProfitDistributionEngine.Calculate(
                profit.DistributableAmount,
                members.Select(x => new ProfitDistributionMemberInput(x.Member.Id, x.ContributionAmount, x.MembershipMonths, x.AverageBalance)),
                method,
                precision);
        }
        catch (ArgumentException ex) { throw new ProfitDistributionValidationException(ex.Message); }
        catch (InvalidOperationException ex) { throw new ProfitDistributionValidationException(ex.Message); }
    }

    private ProfitDistributionResponse BuildPreviewResponse(ProfitDistributionPreviewRequest request, ProfitCalculationResponse profit, IReadOnlyList<EligibleMember> members, IReadOnlyList<ProfitDistributionShare> shares)
    {
        var details = shares.Select(share =>
        {
            var member = members.Single(x => x.Member.Id == share.MemberId);
            return new ProfitDistributionMemberResponse(member.Member.Id, member.Member.MemberNumber, member.Member.FullName,
                member.ContributionAmount, member.MembershipMonths, member.AverageBalance,
                share.EligibilityFactor, share.CalculatedShare, share.DistributedAmount);
        }).ToArray();
        return new ProfitDistributionResponse(0, request.GroupId, request.PeriodStart.Date, request.PeriodEnd.Date,
            profit.NetProfit, profit.ReserveAmount, profit.DistributableAmount, request.DistributionMethod.Trim(), "Preview", null, details, null);
    }

    private static ProfitDistributionResponse Map(ProfitDistribution x, long? transactionId) =>
        new(x.Id, x.GroupId, x.PeriodStart, x.PeriodEnd, x.NetProfit, x.ReserveAmount, x.DistributableAmount,
            x.DistributionMethod, x.Status, x.DistributedDate,
            x.Details.OrderBy(d => d.MemberId).Select(d => new ProfitDistributionMemberResponse(
                d.MemberId, d.Member.MemberNumber, d.Member.FullName, 0m, 0m, 0m,
                d.EligibilityFactor, d.CalculatedShare, d.DistributedAmount)).ToArray(), transactionId);

    private async Task<long?> FindTransactionIdAsync(long distributionId, CancellationToken ct) =>
        await db.FinancialTransactions.AsNoTracking()
            .Where(x => x.ReferenceType == "ProfitDistribution" && x.ReferenceId == distributionId)
            .Select(x => (long?)x.Id).SingleOrDefaultAsync(ct);

    private async Task<FinancialAccount> EnsureSystemAccountAsync(long groupId, string code, string name, string type, long userId, CancellationToken ct)
    {
        var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.GroupId == groupId && x.AccountCode == code, ct);
        if (account is not null) return account;
        account = new FinancialAccount { GroupId = groupId, AccountCode = code, AccountName = name, AccountType = type, IsSystemAccount = true, IsActive = true, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = userId };
        db.FinancialAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }

    private async Task EnsureGroupAsync(long groupId, CancellationToken ct)
    {
        if (!await db.Groups.AnyAsync(x => x.Id == groupId && x.IsActive, ct))
            throw new ProfitDistributionValidationException("The selected group does not exist or is inactive.");
    }

    private static void ValidateRequest(long groupId, DateTime start, DateTime end, string method)
    {
        if (groupId <= 0) throw new ProfitDistributionValidationException("GroupId is required.");
        if (start == default) throw new ProfitDistributionValidationException("Period start is required.");
        if (end == default) throw new ProfitDistributionValidationException("Period end is required.");
        if (end.Date < start.Date) throw new ProfitDistributionValidationException("Period end cannot be earlier than period start.");
        if (!ProfitDistributionMethods.All.Contains(method?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            throw new ProfitDistributionValidationException("Unsupported distribution method. Use Equal, ContributionBased, or Weighted.");
    }

    private static decimal CompletedMonths(DateTime joinDate, DateTime asOfDate)
    {
        if (joinDate > asOfDate) return 0m;
        var months = (asOfDate.Year - joinDate.Year) * 12 + asOfDate.Month - joinDate.Month;
        if (asOfDate.Day < joinDate.Day) months--;
        return Math.Max(0, months);
    }

    private sealed record EligibleMember(Member Member, decimal ContributionAmount, decimal MembershipMonths, decimal AverageBalance);
}
