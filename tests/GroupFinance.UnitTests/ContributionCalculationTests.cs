using GroupFinance.Core.Constants;

namespace GroupFinance.UnitTests;

public sealed class ContributionCalculationTests
{
    [Fact]
    public void LateFee_IsBasedOnRemainingUnpaidContribution()
    {
        var fee = ContributionCalculation.LateFee(5_000m, 3_000m, 1m);

        Assert.Equal(20m, fee);
    }

    [Fact]
    public void LateFee_IsChargedOnce_WhenExistingFeeExists()
    {
        var fee = ContributionCalculation.LateFee(5_000m, 3_500m, 1m, existingLateFeeAmount: 20m);

        Assert.Equal(20m, fee);
    }

    [Fact]
    public void FullyPaidContribution_HasNoNewLateFee()
    {
        var fee = ContributionCalculation.LateFee(5_000m, 5_000m, 1m);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void LateFee_Rounds_AwayFromZero()
    {
        var fee = ContributionCalculation.LateFee(1_000.50m, 0m, 1m);

        Assert.Equal(10.01m, fee);
    }
}
