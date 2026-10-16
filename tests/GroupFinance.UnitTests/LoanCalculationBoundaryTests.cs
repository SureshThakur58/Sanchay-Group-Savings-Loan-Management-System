using GroupFinance.Core.Loans;

namespace GroupFinance.UnitTests;

public sealed class LoanCalculationBoundaryTests
{
    private readonly LoanCalculationService _service = new();

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 100)]
    [InlineData(60, 12.5)]
    public void Boundary_Rates_And_Durations_Produce_Complete_Principal_Recovery(int duration, decimal rate)
    {
        var result = _service.Calculate(new LoanCalculationRequest(
            100_000m, rate, duration, LoanInterestMethods.ReducingBalance,
            new DateTime(2026, 10, 15), 2));

        Assert.Equal(duration, result.Schedule.Count);
        Assert.Equal(100_000m, result.Schedule.Sum(x => x.PrincipalDue));
        Assert.Equal(0m, result.Schedule[^1].ClosingPrincipal);
        Assert.All(result.Schedule, x => Assert.True(x.PrincipalDue >= 0m));
        Assert.All(result.Schedule, x => Assert.True(x.InterestDue >= 0m));
    }

    [Fact]
    public void FirstDueDate_Is_Normalized_To_Date_Only()
    {
        var result = _service.Calculate(new LoanCalculationRequest(
            50_000m, 12m, 3, LoanInterestMethods.ReducingBalance,
            new DateTime(2026, 10, 15, 23, 59, 59), 2));

        Assert.Equal(new DateTime(2026, 10, 15), result.Schedule[0].DueDate);
    }

    [Fact]
    public void Invalid_Monetary_Precision_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() => _service.Calculate(new LoanCalculationRequest(
            100_000m, 12m, 12, LoanInterestMethods.ReducingBalance,
            new DateTime(2026, 10, 15), -1)));

        Assert.Throws<ArgumentException>(() => _service.Calculate(new LoanCalculationRequest(
            100_000m, 12m, 12, LoanInterestMethods.ReducingBalance,
            new DateTime(2026, 10, 15), 7)));
    }

    [Fact]
    public void Missing_First_Due_Date_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() => _service.Calculate(new LoanCalculationRequest(
            100_000m, 12m, 12, LoanInterestMethods.ReducingBalance, default, 2)));
    }
}
