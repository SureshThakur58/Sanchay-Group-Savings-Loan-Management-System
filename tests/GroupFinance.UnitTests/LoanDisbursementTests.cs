using GroupFinance.Core.Loans;

namespace GroupFinance.UnitTests;

public sealed class LoanDisbursementTests
{
    [Fact]
    public void ReducingBalance_DisbursementScheduleMatchesCalculationEngine()
    {
        var service = new LoanCalculationService();
        var result = service.Calculate(new LoanCalculationRequest(100000m, 12m, 12, LoanInterestMethods.ReducingBalance, new DateTime(2026, 10, 15), 2));

        Assert.Equal(12, result.Schedule.Count);
        Assert.Equal(8884.88m, result.MonthlyInstallment);
        Assert.Equal(100000m, result.Schedule.Sum(x => x.PrincipalDue));
        Assert.Equal(106618.53m, result.TotalRepayment);
        Assert.Equal(0m, result.Schedule[^1].ClosingPrincipal);
    }

    [Fact]
    public void Flat_DisbursementScheduleHasExpectedTotal()
    {
        var service = new LoanCalculationService();
        var result = service.Calculate(new LoanCalculationRequest(100000m, 12m, 12, LoanInterestMethods.Flat, new DateTime(2026, 10, 15), 2));

        Assert.Equal(12000m, result.TotalInterest);
        Assert.Equal(112000m, result.TotalRepayment);
        Assert.Equal(9333.33m, result.MonthlyInstallment);
        Assert.Equal(0m, result.Schedule[^1].ClosingPrincipal);
    }
}
