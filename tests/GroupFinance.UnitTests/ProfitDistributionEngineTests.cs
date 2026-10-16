using GroupFinance.Core.InterestProfit;

namespace GroupFinance.UnitTests;

public sealed class ProfitDistributionEngineTests
{
    [Fact]
    public void Equal_Distribution_Splits_Distributable_Profit_And_Reconciles_Remainder_By_Deterministic_Member_Order()
    {
        var result = ProfitDistributionEngine.Calculate(
            100m,
            [
                new ProfitDistributionMemberInput(1, 1000m, 12m, 1000m),
                new ProfitDistributionMemberInput(2, 1000m, 12m, 1000m),
                new ProfitDistributionMemberInput(3, 1000m, 12m, 1000m)
            ],
            "Equal",
            2);

        Assert.Equal(100m, result.Sum(x => x.DistributedAmount));
        Assert.Equal(33.34m, result[0].DistributedAmount);
        Assert.Equal(33.33m, result[1].DistributedAmount);
        Assert.Equal(33.33m, result[2].DistributedAmount);
    }

    [Fact]
    public void Remainder_Goes_To_Largest_Calculated_Share_Not_Last_Row()
    {
        var result = ProfitDistributionEngine.Calculate(
            101m,
            [
                new ProfitDistributionMemberInput(1, 1000m, 12m, 1000m),
                new ProfitDistributionMemberInput(2, 3000m, 12m, 3000m),
                new ProfitDistributionMemberInput(3, 1000m, 12m, 1000m)
            ],
            "ContributionBased",
            0);

        Assert.Equal(101m, result.Sum(x => x.DistributedAmount));
        Assert.Equal(20m, result.Single(x => x.MemberId == 1).DistributedAmount);
        Assert.Equal(61m, result.Single(x => x.MemberId == 2).DistributedAmount);
        Assert.Equal(20m, result.Single(x => x.MemberId == 3).DistributedAmount);
    }

    [Fact]
    public void Contribution_Based_Distribution_Uses_Cumulative_Contribution()
    {
        var result = ProfitDistributionEngine.Calculate(
            1000m,
            [
                new ProfitDistributionMemberInput(1, 1000m, 12m, 1000m),
                new ProfitDistributionMemberInput(2, 3000m, 12m, 3000m)
            ],
            "ContributionBased",
            2);

        Assert.Equal(250m, result.Single(x => x.MemberId == 1).DistributedAmount);
        Assert.Equal(750m, result.Single(x => x.MemberId == 2).DistributedAmount);
    }

    [Fact]
    public void Weighted_Distribution_Uses_All_Three_Eligibility_Factors()
    {
        var result = ProfitDistributionEngine.Calculate(
            1000m,
            [
                new ProfitDistributionMemberInput(1, 1000m, 12m, 1000m),
                new ProfitDistributionMemberInput(2, 2000m, 24m, 2000m)
            ],
            "Weighted",
            2);

        Assert.Equal(1000m, result.Sum(x => x.DistributedAmount));
        // Weighted allocation uses the normalized contribution, membership-month,
        // and average-balance factors together. For these inputs the normalized
        // products are 1/27 and 8/27, so the shares are 1/9 and 8/9.
        Assert.Equal(111.11m, result.Single(x => x.MemberId == 1).DistributedAmount);
        Assert.Equal(888.89m, result.Single(x => x.MemberId == 2).DistributedAmount);
        var member1Factor = result.Single(x => x.MemberId == 1).EligibilityFactor;
        var member2Factor = result.Single(x => x.MemberId == 2).EligibilityFactor;

        // Decimal division can retain a different internal scale even when the
        // mathematical value is the same. Validate the eligibility factors with
        // an explicit decimal tolerance rather than requiring identical scales.
        Assert.InRange(Math.Abs(member1Factor - (1m / 9m)), 0m, 0.000000000000000000000000001m);
        Assert.InRange(Math.Abs(member2Factor - (8m / 9m)), 0m, 0.000000000000000000000000001m);
    }

    [Fact]
    public void Contribution_Based_Distribution_Rejects_Zero_Contribution_Basis()
    {
        Assert.Throws<InvalidOperationException>(() => ProfitDistributionEngine.Calculate(
            100m,
            [
                new ProfitDistributionMemberInput(1, 0m, 12m, 0m),
                new ProfitDistributionMemberInput(2, 0m, 12m, 0m)
            ],
            "ContributionBased",
            2));
    }

    [Fact]
    public void Zero_Distributable_Profit_Returns_Zero_Shares()
    {
        var result = ProfitDistributionEngine.Calculate(
            0m,
            [new ProfitDistributionMemberInput(1, 100m, 12m, 100m)],
            "Equal",
            2);

        Assert.Single(result);
        Assert.Equal(0m, result[0].DistributedAmount);
    }
}
