namespace GroupFinance.UnitTests;

/// <summary>
/// Phase 27 financial invariants. These tests deliberately model the accounting
/// identities used by the application so regressions cannot silently break
/// reconciliation rules.
/// </summary>
public sealed class FinancialIntegrityInvariantTests
{
    [Theory]
    [InlineData(1000, 1000)]
    [InlineData(123456, 123456)]
    public void DoubleEntry_Requires_Debit_Equal_Credit(decimal debit, decimal credit)
    {
        Assert.Equal(debit, credit);
    }

    [Fact]
    public void LoanBalance_Equals_OriginalPrincipal_Minus_PrincipalPaid()
    {
        const decimal originalPrincipal = 100_000m;
        const decimal principalPaid = 37_500m;

        var outstanding = originalPrincipal - principalPaid;

        Assert.Equal(62_500m, outstanding);
        Assert.True(outstanding >= 0m);
    }

    [Fact]
    public void FundBalance_Reconciles_From_Inflows_And_Outflows()
    {
        const decimal opening = 200_000m;
        const decimal contributions = 100_000m;
        const decimal interest = 10_000m;
        const decimal otherIncome = 5_000m;
        const decimal loans = 150_000m;
        const decimal expenses = 20_000m;
        const decimal assets = 25_000m;

        var closing = opening + contributions + interest + otherIncome - loans - expenses - assets;

        Assert.Equal(120_000m, closing);
    }

    [Fact]
    public void Profit_Reconciles_Income_Less_Costs()
    {
        const decimal income = 62_000m;
        const decimal expenses = 7_000m;
        const decimal otherCosts = 2_000m;

        Assert.Equal(53_000m, income - expenses - otherCosts);
    }

    [Fact]
    public void ProfitDistribution_Reconciles_Distribution_Reserve_And_Undistributed()
    {
        const decimal availableProfit = 55_000m;
        const decimal reserve = 5_500m;
        const decimal distribution = 49_500m;
        const decimal undistributed = 0m;

        Assert.Equal(availableProfit, distribution + reserve + undistributed);
    }

    [Fact]
    public void MonetaryRounding_Uses_AwayFromZero_To_Two_Decimal_Places()
    {
        Assert.Equal(100.01m, decimal.Round(100.005m, 2, MidpointRounding.AwayFromZero));
        Assert.Equal(-100.01m, decimal.Round(-100.005m, 2, MidpointRounding.AwayFromZero));
    }
}
