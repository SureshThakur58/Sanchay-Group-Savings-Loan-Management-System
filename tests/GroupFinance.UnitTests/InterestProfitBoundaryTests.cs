using GroupFinance.Core.InterestProfit;

namespace GroupFinance.UnitTests;

public sealed class InterestProfitBoundaryTests
{
    [Fact]
    public void Interest_Period_Includes_Both_Date_Boundaries()
    {
        var result = InterestCalculationEngine.Calculate(
            100_000m,
            [
                new InterestScheduleInput(1, new DateTime(2026, 1, 1), 100_000m, 100m),
                new InterestScheduleInput(2, new DateTime(2026, 1, 31), 99_000m, 90m),
                new InterestScheduleInput(3, new DateTime(2026, 2, 1), 98_000m, 80m)
            ],
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31),
            2);

        Assert.Equal(190m, result.InterestAmount);
    }

    [Fact]
    public void Negative_Interest_Input_Does_Not_Create_Income()
    {
        var result = InterestCalculationEngine.Calculate(
            100_000m,
            [new InterestScheduleInput(1, new DateTime(2026, 1, 15), 100_000m, -50m)],
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31),
            2);

        Assert.Equal(0m, result.InterestAmount);
    }

    [Fact]
    public void Invalid_Profit_Reserve_Rate_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() => ProfitCalculationEngine.Calculate([], -0.01m, 2));
        Assert.Throws<ArgumentException>(() => ProfitCalculationEngine.Calculate([], 100.01m, 2));
    }

    [Fact]
    public void Loss_Does_Not_Create_A_Reserve()
    {
        var result = ProfitCalculationEngine.Calculate(
            [new("LOSS", "Expense", "EXPENSE", 10_000m, 0m)],
            10m,
            2);

        Assert.Equal(-10_000m, result.NetProfit);
        Assert.Equal(0m, result.ReserveAmount);
        Assert.Equal(-10_000m, result.DistributableAmount);
    }
}
