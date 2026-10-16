using GroupFinance.Core.Enums;

namespace GroupFinance.UnitTests;

public sealed class FinancialTransactionFoundationTests
{
    [Fact]
    public void TransactionTypes_Contain_All_Required_Types()
    {
        var expected = new[]
        {
            "CONTRIBUTION", "LOAN_DISBURSEMENT", "LOAN_REPAYMENT", "INTEREST_INCOME",
            "INTEREST_DISTRIBUTION", "EXPENSE", "ASSET_PURCHASE", "ASSET_SALE", "TRANSFER", "ADJUSTMENT"
        };

        Assert.Equal(expected.OrderBy(x => x), FinancialTransactionTypes.All.OrderBy(x => x));
    }
}
