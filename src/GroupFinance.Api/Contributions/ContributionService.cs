using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Entities;
using GroupFinance.Core.Enums;
using GroupFinance.Core.Constants;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Contributions;

public sealed class ContributionValidationException(string message) : Exception(message);

public sealed class ContributionService(GroupFinanceDbContext db, FinancialTransactionService financialTransactions)
{
    private static readonly string[] Frequencies = ["Monthly", "Quarterly", "Half-Yearly", "Yearly", "Custom"];
    private static readonly string[] PaymentMethods = ["Cash", "Bank", "Online Transfer", "Other"];

    public async Task<IReadOnlyCollection<ContributionPlanResponse>> ListPlansAsync(long groupId, bool includeInactive, CancellationToken ct)
    {
        await EnsureConfiguredGroupAsync(groupId, ct);
        var query = db.ContributionPlans.AsNoTracking().Include(x => x.Group).Where(x => x.GroupId == groupId);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        var plans = await query.OrderBy(x => x.Name).ToArrayAsync(ct);
        return plans.Select(MapPlan).ToArray();
    }

    public async Task<ContributionPlanResponse?> GetPlanAsync(long id, CancellationToken ct)
    {
        var plan = await db.ContributionPlans.AsNoTracking().Include(x => x.Group).SingleOrDefaultAsync(x => x.Id == id, ct);
        return plan is null ? null : MapPlan(plan);
    }

    public async Task<ContributionPlanResponse> CreatePlanAsync(CreateContributionPlanRequest request, long userId, CancellationToken ct)
    {
        ValidatePlan(request.Name, request.Amount, request.Frequency, request.IntervalMonths, request.DueDay, request.LateFeeRate);
        await EnsureConfiguredGroupAsync(request.GroupId, ct);
        var name = request.Name.Trim();
        if (await db.ContributionPlans.AnyAsync(x => x.GroupId == request.GroupId && x.Name == name, ct))
            throw new ContributionValidationException("Another contribution plan already uses this name.");

        var now = DateTime.UtcNow;
        var entity = new ContributionPlan
        {
            GroupId = request.GroupId,
            Name = name,
            Amount = RoundMoney(request.Amount),
            Frequency = request.Frequency.Trim(),
            IntervalMonths = ResolveIntervalMonths(request.Frequency, request.IntervalMonths),
            DueDay = request.DueDay,
            LateFeeRate = RoundRate(request.LateFeeRate),
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            CreatedByUserId = userId
        };
        db.ContributionPlans.Add(entity);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        { throw new ContributionValidationException("Another contribution plan already uses this name."); }
        return await GetPlanAsync(entity.Id, ct) ?? throw new InvalidOperationException("The contribution plan was created but could not be loaded.");
    }

    public async Task<ContributionPlanResponse?> UpdatePlanAsync(long id, UpdateContributionPlanRequest request, long userId, CancellationToken ct)
    {
        ValidatePlan(request.Name, request.Amount, request.Frequency, request.IntervalMonths, request.DueDay, request.LateFeeRate);
        var entity = await db.ContributionPlans.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        await EnsureConfiguredGroupAsync(entity.GroupId, ct);
        var name = request.Name.Trim();
        if (await db.ContributionPlans.AnyAsync(x => x.Id != id && x.GroupId == entity.GroupId && x.Name == name, ct))
            throw new ContributionValidationException("Another contribution plan already uses this name.");

        entity.Name = name;
        entity.Amount = RoundMoney(request.Amount);
        entity.Frequency = request.Frequency.Trim();
        entity.IntervalMonths = ResolveIntervalMonths(request.Frequency, request.IntervalMonths);
        entity.DueDay = request.DueDay;
        entity.LateFeeRate = RoundRate(request.LateFeeRate);
        entity.IsActive = request.IsActive;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return await GetPlanAsync(id, ct);
    }

    public async Task<ContributionScheduleGenerationResponse> GenerateSchedulesAsync(GenerateContributionSchedulesRequest request, long userId, CancellationToken ct)
    {
        if (request.FromDate == default || request.ToDate == default) throw new ContributionValidationException("FromDate and ToDate are required.");
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;
        if (to < from) throw new ContributionValidationException("ToDate cannot be earlier than FromDate.");

        var plan = await db.ContributionPlans.Include(x => x.Group).SingleOrDefaultAsync(x => x.Id == request.ContributionPlanId, ct)
            ?? throw new ContributionValidationException("Contribution plan not found.");
        await EnsureConfiguredGroupAsync(plan.GroupId, ct);
        if (!plan.IsActive) throw new ContributionValidationException("Inactive contribution plans cannot generate schedules.");

        var groupAnchor = plan.Group.FormationDate.Date;
        var periods = BuildPeriods(groupAnchor, plan.Frequency, plan.IntervalMonths, from, to);
        if (periods.Count == 0) return new(request.ContributionPlanId, from, to, 0, 0);

        var members = await db.Members.Where(x => x.GroupId == plan.GroupId && x.IsActive).OrderBy(x => x.MemberNumber).ToArrayAsync(ct);
        var periodStarts = periods.Select(x => x.Start).ToArray();
        var existing = await db.ContributionSchedules
            .Where(x => x.ContributionPlanId == plan.Id && periodStarts.Contains(x.PeriodStart))
            .Select(x => new { x.MemberId, x.PeriodStart })
            .ToListAsync(ct);
        var existingKeys = existing.Select(x => $"{x.MemberId}:{x.PeriodStart:O}").ToHashSet(StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        var created = 0;
        foreach (var period in periods)
        {
            foreach (var member in members)
            {
                if (member.JoinDate.Date > period.End.Date) continue;
                if (member.ExitDate.HasValue && member.ExitDate.Value.Date < period.Start.Date) continue;
                var key = $"{member.Id}:{period.Start:O}";
                if (existingKeys.Contains(key)) continue;

                var amount = member.ContributionAmountOverride ?? plan.Amount;
                if (amount <= 0m) continue;
                db.ContributionSchedules.Add(new ContributionSchedule
                {
                    ContributionPlanId = plan.Id,
                    MemberId = member.Id,
                    PeriodStart = period.Start,
                    PeriodEnd = period.End,
                    DueDate = BuildDueDate(period.Start, period.End, plan.DueDay),
                    DueAmount = RoundMoney(amount),
                    PaidAmount = 0m,
                    LateFeeAmount = 0m,
                    WaivedLateFeeAmount = 0m,
                    Status = "Due",
                    CreatedAtUtc = now,
                    CreatedByUserId = userId
                });
                created++;
            }
        }
        await db.SaveChangesAsync(ct);
        return new(request.ContributionPlanId, from, to, created, existing.Count);
    }

    public async Task<IReadOnlyCollection<ContributionScheduleResponse>> ListSchedulesAsync(long groupId, long? memberId, long? planId, DateTime? fromDate, DateTime? toDate, string? status, CancellationToken ct)
    {
        await EnsureConfiguredGroupAsync(groupId, ct);
        if (memberId.HasValue && !await db.Members.AnyAsync(x => x.Id == memberId.Value && x.GroupId == groupId, ct))
            throw new ContributionValidationException("The selected member does not belong to the configured group.");
        if (planId.HasValue && !await db.ContributionPlans.AnyAsync(x => x.Id == planId.Value && x.GroupId == groupId, ct))
            throw new ContributionValidationException("The selected contribution plan does not belong to the configured group.");

        var query = db.ContributionSchedules.AsNoTracking()
            .Include(x => x.ContributionPlan)
            .Include(x => x.Member)
            .Include(x => x.Payments)
            .Where(x => x.ContributionPlan.GroupId == groupId);
        if (memberId.HasValue) query = query.Where(x => x.MemberId == memberId.Value);
        if (planId.HasValue) query = query.Where(x => x.ContributionPlanId == planId.Value);
        if (fromDate.HasValue) query = query.Where(x => x.PeriodStart >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.PeriodStart <= toDate.Value.Date);
        var rows = await query.OrderBy(x => x.DueDate).ThenBy(x => x.Member.MemberNumber).Take(1000).ToArrayAsync(ct);
        var mapped = rows.Select(x => MapSchedule(x, x.Member.MemberNumber, x.Member.FullName));
        var normalizedStatus = status?.Trim();
        if (normalizedStatus is { Length: > 0 } statusValue)
            mapped = mapped.Where(x => x.Status.Equals(statusValue, StringComparison.OrdinalIgnoreCase));
        return mapped.ToArray();
    }

    public async Task<ContributionScheduleResponse?> GetScheduleAsync(long id, CancellationToken ct)
    {
        var row = await db.ContributionSchedules.AsNoTracking()
            .Include(x => x.ContributionPlan)
            .Include(x => x.Member)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : MapSchedule(row, row.Member.MemberNumber, row.Member.FullName);
    }

    public async Task<ContributionPaymentResponse> RecordPaymentAsync(long scheduleId, CreateContributionPaymentRequest request, long userId, CancellationToken ct)
    {
        ValidatePayment(request);
        var executionStrategy = db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var schedule = await db.ContributionSchedules.Include(x => x.ContributionPlan).SingleOrDefaultAsync(x => x.Id == scheduleId, ct)
                ?? throw new ContributionValidationException("Contribution schedule not found.");
            var member = await db.Members.SingleOrDefaultAsync(x => x.Id == schedule.MemberId, ct)
                ?? throw new ContributionValidationException("The member linked to this contribution schedule was not found.");
            await EnsureConfiguredGroupAsync(schedule.ContributionPlan.GroupId, ct);

            // ReferenceNumber is the external request/payment reference when supplied.
            // Treat a repeated reference on the same contribution schedule as a duplicate
            // submission so browser retries or double-clicks cannot post the payment twice.
            var referenceNumber = Normalize(request.ReferenceNumber);
            if (referenceNumber is not null && await db.ContributionPayments.AnyAsync(
                    x => x.ContributionScheduleId == scheduleId &&
                         x.ReferenceNumber == referenceNumber &&
                         x.Status == "Posted", ct))
                throw new ContributionValidationException("A posted contribution payment already exists with this reference number.");
            if (!member.IsActive) throw new ContributionValidationException("Inactive members cannot receive new contribution payments.");
            if (!PaymentMethods.Contains(request.PaymentMethod?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                throw new ContributionValidationException("Unsupported payment method.");

            var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.Id == request.FinancialAccountId && x.GroupId == schedule.ContributionPlan.GroupId, ct)
                ?? throw new ContributionValidationException("The selected financial account does not belong to the configured group.");
            if (!account.IsActive) throw new ContributionValidationException("Inactive financial accounts cannot receive contribution payments.");

            var transactionDate = request.TransactionDate.Kind == DateTimeKind.Unspecified ? request.TransactionDate : request.TransactionDate.ToUniversalTime();
            var effectiveLateFee = CalculateLateFee(schedule, transactionDate.Date);
            if (schedule.LateFeeAmount < effectiveLateFee) schedule.LateFeeAmount = effectiveLateFee;
            var outstanding = CalculateOutstanding(schedule);
            if (outstanding <= 0m) throw new ContributionValidationException("This contribution schedule is already fully paid.");
            if (request.Amount > outstanding) throw new ContributionValidationException($"Payment cannot exceed the outstanding amount of {outstanding:0.00}.");

            var contributionAccount = await EnsureSystemAccountAsync(schedule.ContributionPlan.GroupId, "MEMBER-CONTRIBUTION", "Member Contributions", "Equity", userId, ct);
            var lateFeeAccount = await EnsureSystemAccountAsync(schedule.ContributionPlan.GroupId, "CONTRIBUTION-LATE-FEE", "Contribution Late Fee Income", "Income", userId, ct);

            var feeOutstanding = Math.Max(0m, schedule.LateFeeAmount - schedule.WaivedLateFeeAmount);
            var feePaid = Math.Min(request.Amount, feeOutstanding);
            var contributionPaid = request.Amount - feePaid;
            var lines = new List<FinancialTransactionLineRequest>
            {
                new(account.Id, request.Amount, 0m, $"Contribution payment for {member.MemberNumber}")
            };
            if (contributionPaid > 0m) lines.Add(new(contributionAccount.Id, 0m, contributionPaid, $"Member contribution - {member.MemberNumber}"));
            if (feePaid > 0m) lines.Add(new(lateFeeAccount.Id, 0m, feePaid, $"Contribution late fee - {member.MemberNumber}"));

            var journal = await financialTransactions.CreatePostedInCurrentTransactionAsync(
                new CreateFinancialTransactionRequest(
                    schedule.ContributionPlan.GroupId,
                    member.Id,
                    transactionDate,
                    FinancialTransactionTypes.Contribution,
                    request.Description ?? $"Contribution payment - {member.MemberNumber}",
                    "ContributionPayment",
                    scheduleId,
                    referenceNumber,
                    lines), userId, ct);

            var payment = new ContributionPayment
            {
                ContributionScheduleId = schedule.Id,
                MemberId = member.Id,
                TransactionDate = transactionDate,
                Amount = RoundMoney(request.Amount),
                PaymentMethod = (request.PaymentMethod ?? string.Empty).Trim(),
                FinancialAccountId = account.Id,
                ReferenceNumber = referenceNumber,
                Description = Normalize(request.Description),
                Status = "Posted",
                FinancialTransactionId = journal.Id,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };
            db.ContributionPayments.Add(payment);
            schedule.PaidAmount = RoundMoney(schedule.PaidAmount + request.Amount);
            UpdateScheduleStatus(schedule, transactionDate.Date);
            schedule.ModifiedAtUtc = DateTime.UtcNow;
            schedule.ModifiedByUserId = userId;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                throw new ContributionValidationException("A posted contribution payment already exists with this reference number.");
            }
            await transaction.CommitAsync(ct);
            return MapPayment(payment);
        });
    }

    public async Task<ContributionScheduleResponse?> WaiveLateFeeAsync(long scheduleId, WaiveContributionLateFeeRequest request, long userId, CancellationToken ct)
    {
        var schedule = await db.ContributionSchedules.Include(x => x.ContributionPlan).SingleOrDefaultAsync(x => x.Id == scheduleId, ct);
        if (schedule is null) return null;
        await EnsureConfiguredGroupAsync(schedule.ContributionPlan.GroupId, ct);
        var currentFee = CalculateLateFee(schedule, DateTime.UtcNow.Date);
        if (schedule.LateFeeAmount < currentFee) schedule.LateFeeAmount = currentFee;
        var outstandingFee = Math.Max(0m, schedule.LateFeeAmount - schedule.WaivedLateFeeAmount);
        if (outstandingFee <= 0m) throw new ContributionValidationException("There is no outstanding late fee to waive.");
        var amount = request.Amount ?? outstandingFee;
        if (amount <= 0m || amount > outstandingFee) throw new ContributionValidationException("Waiver amount must be greater than zero and cannot exceed the outstanding late fee.");
        schedule.WaivedLateFeeAmount = RoundMoney(schedule.WaivedLateFeeAmount + amount);
        UpdateScheduleStatus(schedule, DateTime.UtcNow.Date);
        schedule.ModifiedAtUtc = DateTime.UtcNow;
        schedule.ModifiedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return await GetScheduleAsync(scheduleId, ct);
    }

    private async Task EnsureConfiguredGroupAsync(long groupId, CancellationToken ct)
    {
        var group = await db.Groups.AsNoTracking().SingleOrDefaultAsync(x => x.Id == groupId, ct);
        if (group is null) throw new ContributionValidationException("The selected group does not exist.");
        if (!group.IsActive) throw new ContributionValidationException("The selected group is inactive.");
        if (await db.Groups.CountAsync(ct) != 1) throw new ContributionValidationException("Exactly one group must be configured before contributions can be managed.");
    }

    private async Task<FinancialAccount> EnsureSystemAccountAsync(long groupId, string code, string name, string type, long userId, CancellationToken ct)
    {
        var account = await db.FinancialAccounts.SingleOrDefaultAsync(x => x.GroupId == groupId && x.AccountCode == code, ct);
        if (account is not null) return account;
        account = new FinancialAccount { GroupId = groupId, AccountCode = code, AccountName = name, AccountType = type, IsSystemAccount = true, IsActive = true, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = userId };
        db.FinancialAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }

    private static void ValidatePlan(string name, decimal amount, string frequency, int intervalMonths, int dueDay, decimal lateFeeRate)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ContributionValidationException("Contribution plan name is required.");
        if (name.Trim().Length > 100) throw new ContributionValidationException("Contribution plan name cannot exceed 100 characters.");
        if (amount <= 0m) throw new ContributionValidationException("Contribution amount must be greater than zero.");
        if (!Frequencies.Contains(frequency?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)) throw new ContributionValidationException("Unsupported contribution frequency.");
        if (intervalMonths <= 0 || intervalMonths > 120) throw new ContributionValidationException("Contribution interval must be between 1 and 120 months.");
        if (dueDay < 1 || dueDay > 31) throw new ContributionValidationException("Due day must be between 1 and 31.");
        if (lateFeeRate < 0m || lateFeeRate > 100m) throw new ContributionValidationException("Late fee rate must be between 0 and 100 percent.");
    }

    private static int ResolveIntervalMonths(string frequency, int intervalMonths) => frequency.Trim().ToLowerInvariant() switch
    {
        "monthly" => 1,
        "quarterly" => 3,
        "half-yearly" => 6,
        "yearly" => 12,
        _ => intervalMonths
    };

    private static void ValidatePayment(CreateContributionPaymentRequest request)
    {
        if (request.TransactionDate == default) throw new ContributionValidationException("Transaction date is required.");
        if (request.Amount <= 0m) throw new ContributionValidationException("Payment amount must be greater than zero.");
        if (request.FinancialAccountId <= 0) throw new ContributionValidationException("Financial account is required.");
        if (string.IsNullOrWhiteSpace(request.PaymentMethod) || !PaymentMethods.Contains(request.PaymentMethod.Trim(), StringComparer.OrdinalIgnoreCase)) throw new ContributionValidationException("Unsupported payment method.");
    }

    private static decimal CalculateLateFee(ContributionSchedule schedule, DateTime asOfDate)
    {
        if (asOfDate.Date <= schedule.DueDate.Date) return 0m;

        // The locked business rule defines the contribution late fee as a one-time
        // percentage of the remaining unpaid contribution, not the original due amount.
        // Once a fee has been established, preserve it rather than recalculating it daily.
        return ContributionCalculation.LateFee(
            schedule.DueAmount,
            schedule.PaidAmount,
            schedule.ContributionPlan.LateFeeRate,
            schedule.LateFeeAmount);
    }

    private static decimal CalculateOutstanding(ContributionSchedule schedule) => Math.Max(0m, RoundMoney(schedule.DueAmount + schedule.LateFeeAmount - schedule.WaivedLateFeeAmount - schedule.PaidAmount));

    private static void UpdateScheduleStatus(ContributionSchedule schedule, DateTime asOfDate)
    {
        var lateFee = CalculateLateFee(schedule, asOfDate);
        if (schedule.LateFeeAmount < lateFee) schedule.LateFeeAmount = lateFee;
        var outstanding = CalculateOutstanding(schedule);
        schedule.Status = outstanding <= 0m ? "Paid" : schedule.PaidAmount > 0m ? "Partial" : asOfDate.Date > schedule.DueDate.Date ? "Overdue" : "Due";
    }

    private static ContributionScheduleResponse MapSchedule(ContributionSchedule schedule, string memberNumber, string memberName)
    {
        var lateFee = Math.Max(schedule.LateFeeAmount, CalculateLateFee(schedule, DateTime.UtcNow.Date));
        var totalDue = RoundMoney(schedule.DueAmount + lateFee - schedule.WaivedLateFeeAmount);
        var outstanding = Math.Max(0m, RoundMoney(totalDue - schedule.PaidAmount));
        var status = outstanding <= 0m ? "Paid" : schedule.PaidAmount > 0m ? "Partial" : DateTime.UtcNow.Date > schedule.DueDate.Date ? "Overdue" : "Due";
        return new(schedule.Id, schedule.ContributionPlanId, schedule.ContributionPlan.Name, schedule.MemberId, memberNumber, memberName,
            schedule.PeriodStart, schedule.PeriodEnd, schedule.DueDate, schedule.DueAmount, schedule.PaidAmount, lateFee, schedule.WaivedLateFeeAmount,
            totalDue, outstanding, status, schedule.Payments.OrderBy(x => x.TransactionDate).ThenBy(x => x.Id).Select(MapPayment).ToArray());
    }

    private static ContributionPaymentResponse MapPayment(ContributionPayment p) => new(p.Id, p.ContributionScheduleId, p.MemberId, p.TransactionDate, p.Amount, p.PaymentMethod, p.FinancialAccountId, p.ReferenceNumber, p.Description, p.Status, p.FinancialTransactionId, p.CreatedAtUtc, p.CreatedByUserId);
    private static ContributionPlanResponse MapPlan(ContributionPlan p) => new(p.Id, p.GroupId, p.Group.Code, p.Group.Name, p.Name, p.Amount, p.Frequency, p.IntervalMonths, p.DueDay, p.LateFeeRate, p.IsActive, p.CreatedAtUtc, p.CreatedByUserId, p.ModifiedAtUtc, p.ModifiedByUserId);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal RoundRate(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static List<(DateTime Start, DateTime End)> BuildPeriods(DateTime anchor, string frequency, int intervalMonths, DateTime from, DateTime to)
    {
        var interval = ResolveIntervalMonths(frequency, intervalMonths);
        var anchorMonth = new DateTime(anchor.Year, anchor.Month, 1);
        var monthsFromAnchor = ((from.Year - anchorMonth.Year) * 12) + from.Month - anchorMonth.Month;
        var offset = monthsFromAnchor >= 0 ? monthsFromAnchor / interval : -(((-monthsFromAnchor) + interval - 1) / interval);
        var start = anchorMonth.AddMonths(offset * interval);
        while (start.AddMonths(interval).AddDays(-1) < from) start = start.AddMonths(interval);
        var result = new List<(DateTime, DateTime)>();
        while (start <= to)
        {
            var end = start.AddMonths(interval).AddDays(-1);
            if (end >= from && start <= to) result.Add((start, end));
            start = start.AddMonths(interval);
        }
        return result;
    }

    private static DateTime BuildDueDate(DateTime periodStart, DateTime periodEnd, int dueDay)
    {
        var day = Math.Min(dueDay, DateTime.DaysInMonth(periodStart.Year, periodStart.Month));
        var candidate = new DateTime(periodStart.Year, periodStart.Month, day);
        return candidate < periodStart ? periodStart : candidate > periodEnd ? periodEnd : candidate;
    }
}
