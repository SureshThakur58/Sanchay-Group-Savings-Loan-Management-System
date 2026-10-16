namespace GroupFinance.Core.Constants;

public static class ContributionCalculation
{
    /// <summary>Calculates the one-time late fee from the remaining unpaid contribution.</summary>
    public static decimal LateFee(decimal dueAmount, decimal paidAmount, decimal lateFeeRate, decimal existingLateFeeAmount = 0m, int precision = 2)
    {
        if (dueAmount < 0m) throw new ArgumentOutOfRangeException(nameof(dueAmount));
        if (paidAmount < 0m) throw new ArgumentOutOfRangeException(nameof(paidAmount));
        if (lateFeeRate < 0m || lateFeeRate > 100m) throw new ArgumentOutOfRangeException(nameof(lateFeeRate));
        if (existingLateFeeAmount < 0m) throw new ArgumentOutOfRangeException(nameof(existingLateFeeAmount));
        if (precision < 0 || precision > 6) throw new ArgumentOutOfRangeException(nameof(precision));
        if (existingLateFeeAmount > 0m) return existingLateFeeAmount;

        var outstandingContribution = Math.Max(0m, dueAmount - paidAmount);
        return Math.Round(outstandingContribution * lateFeeRate / 100m, precision, MidpointRounding.AwayFromZero);
    }
}
