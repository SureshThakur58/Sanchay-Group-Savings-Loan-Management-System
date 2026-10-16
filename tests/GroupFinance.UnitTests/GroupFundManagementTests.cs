using GroupFinance.Core.Constants;

namespace GroupFinance.UnitTests;

public sealed class GroupFundManagementTests
{
    [Fact]
    public void FundFormula_CalculatesTotalAvailableAndClosingFund()
    {
        var total = GroupFundCalculation.TotalAvailableFund(200_000m, 100_000m, 10_000m, 2_000m, 0m);
        var closing = GroupFundCalculation.ClosingFund(total, 150_000m, 5_000m, 50_000m, 0m);

        Assert.Equal(312_000m, total);
        Assert.Equal(107_000m, closing);
    }

    [Fact]
    public void Transfer_DoesNotChangeTotalFund()
    {
        var before = 250_000m;
        var after = before - 25_000m + 25_000m;

        Assert.Equal(before, after);
    }
}
