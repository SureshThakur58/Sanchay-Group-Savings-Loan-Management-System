using System.Diagnostics;
using GroupFinance.Core.InterestProfit;

namespace GroupFinance.UnitTests;

public sealed class PerformanceSmokeTests
{
    [Fact]
    public void ProfitDistribution_Processes_10000_Members_And_Reconciles()
    {
        var members = Enumerable.Range(1, 10_000)
            .Select(id => new ProfitDistributionMemberInput(id, 1_000m + id, 12m, 1_000m + id))
            .ToArray();

        var stopwatch = Stopwatch.StartNew();
        var result = ProfitDistributionEngine.Calculate(1_000_000m, members, "ContributionBased", 2);
        stopwatch.Stop();

        Assert.Equal(10_000, result.Count);
        Assert.Equal(1_000_000m, result.Sum(x => x.DistributedAmount));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Pure-engine distribution took {stopwatch.Elapsed}.");
    }
}
