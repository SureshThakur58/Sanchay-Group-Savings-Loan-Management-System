namespace GroupFinance.Core.Loans;

public sealed record LoanRepaymentAllocationInput(
    long ScheduleId,
    int InstallmentNumber,
    DateTime DueDate,
    decimal PrincipalOutstanding,
    decimal InterestOutstanding,
    decimal PenaltyOutstanding);

public sealed record LoanRepaymentAllocationLine(
    long ScheduleId,
    int InstallmentNumber,
    DateTime DueDate,
    decimal PenaltyAllocated,
    decimal InterestAllocated,
    decimal PrincipalAllocated);

public sealed record LoanRepaymentAllocationResult(
    decimal PenaltyAllocated,
    decimal InterestAllocated,
    decimal PrincipalAllocated,
    decimal OverpaymentAmount,
    IReadOnlyList<LoanRepaymentAllocationLine> Allocations);

/// <summary>Pure repayment allocation engine. Priority is penalty, interest, then principal.
/// Future interest is never collected during prepayment; remaining funds reduce future principal.</summary>
public static class LoanRepaymentAllocationCalculator
{
    public static LoanRepaymentAllocationResult Calculate(
        decimal amount,
        DateTime paymentDate,
        IReadOnlyList<LoanRepaymentAllocationInput> schedules)
    {
        if (amount <= 0m) throw new ArgumentException("Repayment amount must be greater than zero.", nameof(amount));
        var remaining = amount;
        decimal penalty = 0m, interest = 0m, principal = 0m;
        var lines = new List<LoanRepaymentAllocationLine>();
        var ordered = schedules.OrderBy(x => x.DueDate).ThenBy(x => x.InstallmentNumber).ToArray();

        var dueSchedules = ordered.Where(x => x.DueDate.Date <= paymentDate.Date).ToArray();

        // Apply payment priority globally across all due schedules:
        // penalty first, then interest, then principal. This allows a partial
        // payment to clear the higher-priority components of multiple due
        // installments before reducing principal.
        var allocations = dueSchedules.ToDictionary(
            x => x.ScheduleId,
            x => new decimal[3]);

        foreach (var schedule in dueSchedules)
        {
            if (remaining <= 0m) break;
            var p = Take(ref remaining, schedule.PenaltyOutstanding);
            penalty += p;
            allocations[schedule.ScheduleId][0] = p;
        }

        foreach (var schedule in dueSchedules)
        {
            if (remaining <= 0m) break;
            var i = Take(ref remaining, schedule.InterestOutstanding);
            interest += i;
            allocations[schedule.ScheduleId][1] = i;
        }

        foreach (var schedule in dueSchedules)
        {
            if (remaining <= 0m) break;
            var pr = Take(ref remaining, schedule.PrincipalOutstanding);
            principal += pr;
            allocations[schedule.ScheduleId][2] = pr;
        }

        foreach (var schedule in dueSchedules)
        {
            var allocation = allocations[schedule.ScheduleId];
            if (allocation[0] > 0m || allocation[1] > 0m || allocation[2] > 0m)
            {
                lines.Add(new LoanRepaymentAllocationLine(
                    schedule.ScheduleId,
                    schedule.InstallmentNumber,
                    schedule.DueDate,
                    allocation[0],
                    allocation[1],
                    allocation[2]));
            }
        }

        foreach (var schedule in ordered.Where(x => x.DueDate.Date > paymentDate.Date))
        {
            if (remaining <= 0m) break;
            var pr = Take(ref remaining, schedule.PrincipalOutstanding);
            if (pr > 0m)
            {
                principal += pr;
                lines.Add(new LoanRepaymentAllocationLine(schedule.ScheduleId, schedule.InstallmentNumber, schedule.DueDate, 0m, 0m, pr));
            }
        }

        return new LoanRepaymentAllocationResult(penalty, interest, principal, remaining, lines);
    }

    private static decimal Take(ref decimal remaining, decimal outstanding)
    {
        var amount = Math.Min(Math.Max(0m, remaining), Math.Max(0m, outstanding));
        remaining -= amount;
        return amount;
    }
}
