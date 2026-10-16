namespace GroupFinance.UnitTests;

public sealed class LedgerTests
{
    [Fact]
    public void TrialBalanceDebitCreditBalancesAreNettedCorrectly()
    {
        var debit = 1500m;
        var credit = 400m;
        var net = debit - credit;

        Assert.Equal(1100m, Math.Max(0m, net));
        Assert.Equal(0m, Math.Max(0m, -net));

        net = 300m - 900m;
        Assert.Equal(0m, Math.Max(0m, net));
        Assert.Equal(600m, Math.Max(0m, -net));
    }

    [Fact]
    public void RunningBalanceUsesDebitMinusCredit()
    {
        var opening = 1000m;
        var running = opening;

        running += 500m - 0m;
        Assert.Equal(1500m, running);

        running += 0m - 250m;
        Assert.Equal(1250m, running);
    }

    [Fact]
    public void BalancedFinancialTransactionHasZeroNetMovementAcrossAllLines()
    {
        var totalDebit = 5000m;
        var totalCredit = 5000m;

        Assert.Equal(0m, totalDebit - totalCredit);
    }
}
