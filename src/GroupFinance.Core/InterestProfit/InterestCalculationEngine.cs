namespace GroupFinance.Core.InterestProfit;

public sealed record InterestScheduleInput(
    long ScheduleId,
    DateTime DueDate,
    decimal OpeningPrincipal,
    decimal InterestDue);

public sealed record InterestCalculationResult(
    decimal OpeningPrincipal,
    decimal InterestAmount);

public static class InterestCalculationEngine
{
    public static InterestCalculationResult Calculate(
        decimal openingPrincipal,
        IEnumerable<InterestScheduleInput> schedules,
        DateTime periodStart,
        DateTime periodEnd,
        int precision)
    {
        if (periodStart == default) throw new ArgumentException("Period start is required.", nameof(periodStart));
        if (periodEnd == default) throw new ArgumentException("Period end is required.", nameof(periodEnd));
        if (periodEnd.Date < periodStart.Date) throw new ArgumentException("Period end cannot be earlier than period start.", nameof(periodEnd));
        if (precision < 0 || precision > 6) throw new ArgumentOutOfRangeException(nameof(precision));
        if (openingPrincipal < 0m) throw new ArgumentException("Opening principal cannot be negative.", nameof(openingPrincipal));

        var start = periodStart.Date;
        var end = periodEnd.Date;
        var inPeriod = schedules
            .Where(x => x.DueDate.Date >= start && x.DueDate.Date <= end)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.ScheduleId)
            .ToArray();

        var amount = RoundMoney(inPeriod.Sum(x => Math.Max(0m, x.InterestDue)), precision);
        return new InterestCalculationResult(RoundMoney(openingPrincipal, precision), amount);
    }

    private static decimal RoundMoney(decimal value, int precision) =>
        Math.Round(value, precision, MidpointRounding.AwayFromZero);
}
