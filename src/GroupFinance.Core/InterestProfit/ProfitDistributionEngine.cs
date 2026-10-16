namespace GroupFinance.Core.InterestProfit;

public sealed record ProfitDistributionMemberInput(
    long MemberId,
    decimal ContributionAmount,
    decimal MembershipMonths,
    decimal AverageBalance);

public sealed record ProfitDistributionShare(
    long MemberId,
    decimal EligibilityFactor,
    decimal CalculatedShare,
    decimal DistributedAmount);

public static class ProfitDistributionEngine
{
    public static IReadOnlyList<ProfitDistributionShare> Calculate(
        decimal distributableAmount,
        IEnumerable<ProfitDistributionMemberInput> members,
        string method,
        int precision)
    {
        if (distributableAmount < 0m) throw new ArgumentException("Distributable amount cannot be negative.", nameof(distributableAmount));
        if (precision < 0 || precision > 6) throw new ArgumentOutOfRangeException(nameof(precision));

        var rows = members.ToArray();
        if (rows.Length == 0) throw new InvalidOperationException("At least one eligible member is required.");
        if (distributableAmount == 0m)
            return rows.Select(x => new ProfitDistributionShare(x.MemberId, 0m, 0m, 0m)).ToArray();

        var normalizedMethod = method?.Trim() ?? string.Empty;
        var weights = normalizedMethod.Equals("Equal", StringComparison.OrdinalIgnoreCase)
            ? rows.Select(x => (x.MemberId, Weight: 1m)).ToArray()
            : normalizedMethod.Equals("ContributionBased", StringComparison.OrdinalIgnoreCase)
                ? rows.Select(x => (x.MemberId, Weight: Math.Max(0m, x.ContributionAmount))).ToArray()
                : normalizedMethod.Equals("Weighted", StringComparison.OrdinalIgnoreCase)
                    ? BuildWeightedWeights(rows)
                    : throw new ArgumentException("Unsupported distribution method.", nameof(method));

        var totalWeight = weights.Sum(x => x.Weight);
        if (totalWeight <= 0m)
            throw new InvalidOperationException("The selected distribution method has no positive eligibility basis for the eligible members.");

        // Round each member's calculated share first, then assign any rounding remainder
        // to the member with the largest calculated share. If the largest calculated
        // shares are tied, the smallest member id is used as the deterministic tie-breaker.
        var roundedShares = weights
            .Select(x => new
            {
                x.MemberId,
                x.Weight,
                RawShare = distributableAmount * x.Weight / totalWeight,
                RoundedShare = RoundMoney(distributableAmount * x.Weight / totalWeight, precision)
            })
            .ToArray();

        var roundedTotal = RoundMoney(roundedShares.Sum(x => x.RoundedShare), precision);
        var remainder = RoundMoney(distributableAmount - roundedTotal, precision);
        if (remainder != 0m)
        {
            var remainderRecipient = roundedShares
                .OrderByDescending(x => x.RawShare)
                .ThenBy(x => x.MemberId)
                .First();

            roundedShares = roundedShares
                .Select(x => x.MemberId == remainderRecipient.MemberId
                    ? new { x.MemberId, x.Weight, x.RawShare, RoundedShare = RoundMoney(x.RoundedShare + remainder, precision) }
                    : x)
                .ToArray();
        }

        return roundedShares
            .Select(x =>
            {
                var factor = totalWeight == 0m ? 0m : x.Weight / totalWeight;
                return new ProfitDistributionShare(x.MemberId, factor, x.RoundedShare, x.RoundedShare);
            })
            .ToArray();
    }

    private static (long MemberId, decimal Weight)[] BuildWeightedWeights(ProfitDistributionMemberInput[] rows)
    {
        var totalContribution = rows.Sum(x => Math.Max(0m, x.ContributionAmount));
        var totalMembership = rows.Sum(x => Math.Max(0m, x.MembershipMonths));
        var totalAverageBalance = rows.Sum(x => Math.Max(0m, x.AverageBalance));

        return rows.Select(x =>
        {
            var contributionFactor = totalContribution > 0m ? Math.Max(0m, x.ContributionAmount) / totalContribution : 0m;
            var membershipFactor = totalMembership > 0m ? Math.Max(0m, x.MembershipMonths) / totalMembership : 0m;
            var balanceFactor = totalAverageBalance > 0m ? Math.Max(0m, x.AverageBalance) / totalAverageBalance : 0m;
            return (x.MemberId, Weight: contributionFactor * membershipFactor * balanceFactor);
        }).ToArray();
    }

    private static decimal RoundMoney(decimal value, int precision) =>
        Math.Round(value, precision, MidpointRounding.AwayFromZero);
}
