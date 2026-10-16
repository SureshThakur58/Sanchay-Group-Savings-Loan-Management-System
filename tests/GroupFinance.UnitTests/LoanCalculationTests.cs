using GroupFinance.Core.Loans;

namespace GroupFinance.UnitTests;

public sealed class LoanCalculationTests
{
    private readonly LoanCalculationService _service = new();

    [Fact]
    public void ReducingBalance_100000_12Months_12Percent_GeneratesBalancedSchedule()
    {
        var result = _service.Calculate(new LoanCalculationRequest(
            100000m, 12m, 12, LoanInterestMethods.ReducingBalance, new DateTime(2026, 10, 15), 2));

        Assert.Equal(100000m, result.PrincipalAmount);
        Assert.Equal(8884.88m, result.MonthlyInstallment);
        Assert.Equal(12, result.Schedule.Count);
        Assert.Equal(100000m, result.Schedule.Sum(x => x.PrincipalDue));
        Assert.Equal(0m, result.Schedule[^1].ClosingPrincipal);
        Assert.Equal(new DateTime(2026, 10, 15), result.Schedule[0].DueDate);
        Assert.Equal(new DateTime(2027, 9, 15), result.Schedule[^1].DueDate);
        Assert.Equal(8884.88m, result.Schedule[0].TotalDue);
        Assert.Equal(1000m, result.Schedule[0].InterestDue);
        Assert.Equal(7884.88m, result.Schedule[0].PrincipalDue);
    }

    [Fact]
    public void Flat_100000_12Months_12Percent_MatchesRequirementExample()
    {
        var result = _service.Calculate(new LoanCalculationRequest(
            100000m, 12m, 12, LoanInterestMethods.Flat, new DateTime(2026, 10, 15), 2));

        Assert.Equal(12000m, result.TotalInterest);
        Assert.Equal(112000m, result.TotalRepayment);
        Assert.Equal(9333.33m, result.MonthlyInstallment);
        Assert.Equal(100000m, result.Schedule.Sum(x => x.PrincipalDue));
        Assert.Equal(12000m, result.Schedule.Sum(x => x.InterestDue));
        Assert.Equal(112000m, result.Schedule.Sum(x => x.TotalDue));
        Assert.Equal(0m, result.Schedule[^1].ClosingPrincipal);
    }

    [Fact]
    public void ZeroInterest_RepaysOnlyPrincipal_WithFinalRoundingAdjustment()
    {
        var result = _service.Calculate(new LoanCalculationRequest(
            100000m, 0m, 12, LoanInterestMethods.ReducingBalance, new DateTime(2026, 10, 15), 2));

        Assert.Equal(0m, result.TotalInterest);
        Assert.Equal(100000m, result.TotalRepayment);
        Assert.Equal(100000m, result.Schedule.Sum(x => x.PrincipalDue));
        Assert.Equal(0m, result.Schedule.Sum(x => x.InterestDue));
        Assert.Equal(0m, result.Schedule[^1].ClosingPrincipal);
    }

    [Fact]
    public void InvalidInputs_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => _service.Calculate(new LoanCalculationRequest(
            0m, 12m, 12, LoanInterestMethods.ReducingBalance, DateTime.Today, 2)));
        Assert.Throws<ArgumentException>(() => _service.Calculate(new LoanCalculationRequest(
            100000m, 12m, 0, LoanInterestMethods.ReducingBalance, DateTime.Today, 2)));
        Assert.Throws<ArgumentException>(() => _service.Calculate(new LoanCalculationRequest(
            100000m, 12m, 12, "Unknown", DateTime.Today, 2)));
    }

    [Fact]
    public void UnsupportedInterestRateAbove100_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => _service.Calculate(new LoanCalculationRequest(
            100000m, 100.01m, 12, LoanInterestMethods.ReducingBalance, DateTime.Today, 2)));
    }
}
