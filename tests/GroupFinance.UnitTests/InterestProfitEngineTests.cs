using GroupFinance.Core.InterestProfit;

namespace GroupFinance.UnitTests;

public sealed class InterestProfitEngineTests
{
    [Fact]
    public void InterestCalculation_Sums_Schedule_Interest_In_Period()
    {
        var result = InterestCalculationEngine.Calculate(
            100000m,
            [
                new InterestScheduleInput(1, new DateTime(2026, 1, 15), 100000m, 1000m),
                new InterestScheduleInput(2, new DateTime(2026, 2, 15), 90000m, 900m),
                new InterestScheduleInput(3, new DateTime(2026, 3, 15), 80000m, 800m)
            ],
            new DateTime(2026, 1, 1),
            new DateTime(2026, 2, 28),
            2);

        Assert.Equal(100000m, result.OpeningPrincipal);
        Assert.Equal(1900m, result.InterestAmount);
    }

    [Fact]
    public void InterestCalculation_Excludes_Schedules_Outside_Period()
    {
        var result = InterestCalculationEngine.Calculate(
            50000m,
            [
                new InterestScheduleInput(1, new DateTime(2026, 1, 15), 50000m, 500m),
                new InterestScheduleInput(2, new DateTime(2026, 3, 15), 49000m, 490m)
            ],
            new DateTime(2026, 2, 1),
            new DateTime(2026, 2, 28),
            2);

        Assert.Equal(50000m, result.OpeningPrincipal);
        Assert.Equal(0m, result.InterestAmount);
    }

    [Fact]
    public void ProfitCalculation_Matches_Requirement_Example()
    {
        var result = ProfitCalculationEngine.Calculate(
            [
                new("LOAN-INTEREST-INCOME", "Income", "LOAN_REPAYMENT", 0m, 50000m),
                new("INVESTMENT-INCOME", "Income", "ADJUSTMENT", 0m, 10000m),
                new("OTHER-INCOME", "Income", "ADJUSTMENT", 0m, 2000m),
                new("EXP-00000001", "Expense", "EXPENSE", 7000m, 0m)
            ],
            10m,
            2);

        Assert.Equal(50000m, result.LoanInterestIncome);
        Assert.Equal(10000m, result.InvestmentIncome);
        Assert.Equal(2000m, result.OtherIncome);
        Assert.Equal(62000m, result.TotalIncome);
        Assert.Equal(7000m, result.Expenses);
        Assert.Equal(0m, result.OtherCosts);
        Assert.Equal(55000m, result.NetProfit);
        Assert.Equal(5500m, result.ReserveAmount);
        Assert.Equal(49500m, result.DistributableAmount);
    }

    [Fact]
    public void ProfitCalculation_Treats_NonExpense_ExpenseAccount_Postings_As_OtherCosts()
    {
        var result = ProfitCalculationEngine.Calculate(
            [new("ASSET-SALE-LOSS", "Expense", "ASSET_SALE", 200m, 0m)],
            10m,
            2);

        Assert.Equal(0m, result.Expenses);
        Assert.Equal(200m, result.OtherCosts);
        Assert.Equal(-200m, result.NetProfit);
        Assert.Equal(0m, result.ReserveAmount);
        Assert.Equal(-200m, result.DistributableAmount);
    }

    [Fact]
    public void ProfitCalculation_Rounds_AwayFromZero()
    {
        var result = ProfitCalculationEngine.Calculate(
            [new("LOAN-INTEREST-INCOME", "Income", "LOAN_REPAYMENT", 0m, 100.005m)],
            10m,
            2);

        Assert.Equal(100.01m, result.LoanInterestIncome);
        Assert.Equal(100.01m, result.NetProfit);
    }
}
