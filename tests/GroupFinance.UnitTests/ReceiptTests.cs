using GroupFinance.Api.Receipts;

namespace GroupFinance.UnitTests;

public sealed class ReceiptTests
{
    [Theory]
    [InlineData("CONTRIBUTION", "Contribution Receipt")]
    [InlineData("LOAN_DISBURSEMENT", "Loan Disbursement Receipt")]
    [InlineData("LOAN_REPAYMENT", "Loan Repayment Receipt")]
    [InlineData("EXPENSE", "Expense Voucher")]
    [InlineData("ASSET_PURCHASE", "Asset Purchase Voucher")]
    [InlineData("INTEREST_DISTRIBUTION", "Profit Distribution Voucher")]
    public void Resolve_supported_transaction_type_returns_expected_receipt(string transactionType, string expected)
    {
        Assert.Equal(expected, ReceiptTypeResolver.Resolve(transactionType));
    }

    [Theory]
    [InlineData("TRANSFER")]
    [InlineData("ADJUSTMENT")]
    [InlineData("UNKNOWN")]
    public void Resolve_unsupported_transaction_type_returns_null(string transactionType)
    {
        Assert.Null(ReceiptTypeResolver.Resolve(transactionType));
    }
}
