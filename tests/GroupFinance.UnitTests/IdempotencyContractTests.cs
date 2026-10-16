using GroupFinance.Api.Contributions;
using GroupFinance.Api.LoanRepayments;

namespace GroupFinance.UnitTests;

public sealed class IdempotencyContractTests
{
    [Fact]
    public void ContributionPayment_Request_Contains_External_Reference()
    {
        var request = new CreateContributionPaymentRequest(
            new DateTime(2026, 9, 15), 5_000m, "Cash", 1, "RCPT-1001", "Contribution");

        Assert.Equal("RCPT-1001", request.ReferenceNumber);
    }

    [Fact]
    public void LoanRepayment_Request_Contains_External_Reference()
    {
        var request = new LoanRepaymentRequest(
            1, new DateTime(2026, 9, 15), 5_000m, "Cash", "PAY-1001", "Repayment");

        Assert.Equal("PAY-1001", request.ReferenceNumber);
    }
}
