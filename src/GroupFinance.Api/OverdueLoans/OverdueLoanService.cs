using System.Data;
using GroupFinance.Core.Entities;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.OverdueLoans;

public sealed class OverdueLoanValidationException(string message) : Exception(message);

public sealed class OverdueLoanService(GroupFinanceDbContext db)
{
    public async Task<OverdueEvaluationResponse> EvaluateAsync(long groupId, DateTime asOfDate, CancellationToken ct)
    {
        if (groupId <= 0) throw new OverdueLoanValidationException("GroupId is required.");
        if (asOfDate == default) throw new OverdueLoanValidationException("As-of date is required.");

        var groupExists = await db.Groups.AnyAsync(x => x.Id == groupId, ct);
        if (!groupExists) throw new OverdueLoanValidationException("Group not found.");

        var settings = await db.GroupSettings.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == groupId, ct)
            ?? throw new OverdueLoanValidationException("The configured group does not have loan settings.");

        var precision = Math.Clamp(settings.MonetaryPrecision, 0, 6);
        var date = asOfDate.Date;

        var schedules = await db.LoanSchedules
            .Include(x => x.Loan)
            .Where(x => x.Loan.GroupId == groupId && x.Loan.Status == "Active" && x.DueDate.Date < date)
            .ToListAsync(ct);

        var changed = false;
        foreach (var schedule in schedules)
        {
            var outstandingPrincipal = Math.Max(0m, schedule.PrincipalDue - schedule.PrincipalPaid);
            var outstandingInterest = Math.Max(0m, schedule.InterestDue - schedule.InterestPaid);
            var outstandingBeforePenalty = outstandingPrincipal + outstandingInterest;
            var outstandingPenalty = Math.Max(0m, schedule.PenaltyDue - schedule.PenaltyPaid);

            if (outstandingBeforePenalty <= 0m && outstandingPenalty <= 0m)
            {
                if (schedule.Status != "Paid")
                {
                    schedule.Status = "Paid";
                    changed = true;
                }
                continue;
            }

            // Late fee is applied once when an installment first becomes overdue.
            // Phase 0's configured 1% late-fee rate is applied to the remaining
            // principal + interest of that installment, not compounded daily.
            if (schedule.PenaltyDue <= 0m && outstandingBeforePenalty > 0m && settings.ContributionLateFeeRate > 0m)
            {
                schedule.PenaltyDue = RoundMoney(outstandingBeforePenalty * settings.ContributionLateFeeRate / 100m, precision);
                schedule.TotalDue = RoundMoney(schedule.PrincipalDue + schedule.InterestDue + schedule.PenaltyDue, precision);
                changed = true;
            }

            var outstandingAfterPenalty = Math.Max(0m, schedule.PrincipalDue - schedule.PrincipalPaid)
                + Math.Max(0m, schedule.InterestDue - schedule.InterestPaid)
                + Math.Max(0m, schedule.PenaltyDue - schedule.PenaltyPaid);

            var targetStatus = outstandingAfterPenalty <= 0m ? "Paid" : "Overdue";
            if (!string.Equals(schedule.Status, targetStatus, StringComparison.Ordinal))
            {
                schedule.Status = targetStatus;
                changed = true;
            }
            schedule.ModifiedAtUtc = DateTime.UtcNow;
        }

        if (changed)
            await db.SaveChangesAsync(ct);

        return await BuildEvaluationAsync(groupId, date, precision, ct);
    }

    public async Task<IReadOnlyCollection<OverdueLoanSummaryResponse>> ListAsync(long groupId, DateTime? asOfDate, CancellationToken ct)
    {
        var date = (asOfDate ?? DateTime.Today).Date;
        await EvaluateAsync(groupId, date, ct);
        return await QuerySummariesAsync(groupId, date, ct);
    }

    public async Task<OverdueLoanSummaryResponse?> GetLoanAsync(long loanId, DateTime? asOfDate, CancellationToken ct)
    {
        var loan = await db.Loans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == loanId, ct);
        if (loan is null) return null;

        var date = (asOfDate ?? DateTime.Today).Date;
        await EvaluateAsync(loan.GroupId, date, ct);
        return (await QuerySummariesAsync(loan.GroupId, date, ct)).SingleOrDefault(x => x.LoanId == loanId);
    }

    public async Task<IReadOnlyCollection<OverdueLoanScheduleResponse>?> GetInstallmentsAsync(long loanId, DateTime? asOfDate, CancellationToken ct)
    {
        var loan = await db.Loans.AsNoTracking().Include(x => x.Member).SingleOrDefaultAsync(x => x.Id == loanId, ct);
        if (loan is null) return null;

        var date = (asOfDate ?? DateTime.Today).Date;
        await EvaluateAsync(loan.GroupId, date, ct);

        // MapSchedule needs Loan.LoanNumber and Loan.MemberId.
        // This query loads Loan explicitly because LoanSchedule.Loan is a required
        // navigation but is not automatically populated by EF Core. Without the
        // Include, the overdue-installment endpoint can throw a NullReferenceException
        // when MapSchedule accesses x.Loan.
        var schedules = await db.LoanSchedules.AsNoTracking()
            .Include(x => x.Loan)
            .Where(x => x.LoanId == loanId && x.DueDate.Date < date && x.Status == "Overdue")
            .OrderBy(x => x.DueDate).ThenBy(x => x.InstallmentNumber)
            .ToListAsync(ct);

        return schedules
            .Select(x => MapSchedule(x, loan.Member.MemberNumber, loan.Member.FullName, date))
            .ToArray();
    }

    private async Task<OverdueEvaluationResponse> BuildEvaluationAsync(long groupId, DateTime asOfDate, int precision, CancellationToken ct)
    {
        var overdue = await db.LoanSchedules.AsNoTracking()
            .Include(x => x.Loan).ThenInclude(x => x.Member)
            .Where(x => x.Loan.GroupId == groupId && x.Loan.Status == "Active" && x.Status == "Overdue" && x.DueDate.Date < asOfDate)
            .ToListAsync(ct);

        return new OverdueEvaluationResponse(
            groupId,
            asOfDate,
            overdue.Count,
            overdue.Select(x => x.LoanId).Distinct().Count(),
            RoundMoney(overdue.Sum(x => Math.Max(0m, x.PrincipalDue - x.PrincipalPaid)), precision),
            RoundMoney(overdue.Sum(x => Math.Max(0m, x.InterestDue - x.InterestPaid)), precision),
            RoundMoney(overdue.Sum(x => Math.Max(0m, x.PenaltyDue - x.PenaltyPaid)), precision),
            RoundMoney(overdue.Sum(x =>
                Math.Max(0m, x.PrincipalDue - x.PrincipalPaid) +
                Math.Max(0m, x.InterestDue - x.InterestPaid) +
                Math.Max(0m, x.PenaltyDue - x.PenaltyPaid)), precision));
    }

    private async Task<IReadOnlyCollection<OverdueLoanSummaryResponse>> QuerySummariesAsync(long groupId, DateTime asOfDate, CancellationToken ct)
    {
        var schedules = await db.LoanSchedules.AsNoTracking()
            .Include(x => x.Loan).ThenInclude(x => x.Member)
            .Where(x => x.Loan.GroupId == groupId && x.Loan.Status == "Active" && x.Status == "Overdue" && x.DueDate.Date < asOfDate)
            .OrderBy(x => x.LoanId).ThenBy(x => x.DueDate).ThenBy(x => x.InstallmentNumber)
            .ToListAsync(ct);

        return schedules.GroupBy(x => x.LoanId).Select(g =>
        {
            var first = g.First();
            var items = g.Select(x => MapSchedule(x, first.Loan.Member.MemberNumber, first.Loan.Member.FullName, asOfDate)).ToArray();
            return new OverdueLoanSummaryResponse(
                first.LoanId,
                first.Loan.LoanNumber,
                first.Loan.GroupId,
                first.Loan.MemberId,
                first.Loan.Member.MemberNumber,
                first.Loan.Member.FullName,
                items.Length,
                items.Max(x => x.DaysOverdue),
                items.Sum(x => x.OutstandingPrincipal),
                items.Sum(x => x.OutstandingInterest),
                items.Sum(x => x.OutstandingPenalty),
                items.Sum(x => x.OutstandingAmount),
                items.Min(x => x.DueDate),
                "Overdue",
                items);
        }).ToArray();
    }

    private static OverdueLoanScheduleResponse MapSchedule(LoanSchedule x, string memberNumber, string memberName, DateTime asOfDate)
    {
        var principal = Math.Max(0m, x.PrincipalDue - x.PrincipalPaid);
        var interest = Math.Max(0m, x.InterestDue - x.InterestPaid);
        var penalty = Math.Max(0m, x.PenaltyDue - x.PenaltyPaid);
        return new OverdueLoanScheduleResponse(
            x.Id, x.LoanId, x.Loan.LoanNumber, x.Loan.MemberId, memberNumber, memberName,
            x.InstallmentNumber, x.DueDate, Math.Max(0, (asOfDate.Date - x.DueDate.Date).Days),
            x.PrincipalDue, x.InterestDue, x.PenaltyDue,
            x.PrincipalPaid, x.InterestPaid, x.PenaltyPaid,
            principal, interest, penalty, principal + interest + penalty, "Overdue");
    }

    private static decimal RoundMoney(decimal value, int precision) => decimal.Round(value, precision, MidpointRounding.AwayFromZero);
}
