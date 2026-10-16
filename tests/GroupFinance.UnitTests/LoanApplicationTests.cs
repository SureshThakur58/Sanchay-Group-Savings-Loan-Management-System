using GroupFinance.Core.Loans;

namespace GroupFinance.UnitTests;

public sealed class LoanApplicationTests
{
    [Fact]
    public void StatusCatalog_Matches_Required_Workflow()
    {
        Assert.Equal(new[] { "Draft", "Submitted", "Approved", "Rejected", "Disbursed", "Closed", "Cancelled" }, LoanApplicationStatuses.All);
    }

    [Fact]
    public void EligibleMember_Passes_DefaultLoanRules()
    {
        var result = LoanEligibilityEvaluator.Evaluate(new LoanEligibilityInput(
            new DateTime(2025, 1, 15), new DateTime(2026, 9, 15), true,
            50_000m, 120_000m, 0, 6, 3m, 1));

        Assert.True(result.IsEligible);
        Assert.Equal(20, result.MembershipMonths);
        Assert.Equal(150_000m, result.MaximumEligibleAmount);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Request_Above_Contribution_Multiplier_Is_Rejected()
    {
        var result = LoanEligibilityEvaluator.Evaluate(new LoanEligibilityInput(
            new DateTime(2025, 1, 15), new DateTime(2026, 9, 15), true,
            50_000m, 160_000m, 0, 6, 3m, 1));

        Assert.False(result.IsEligible);
        Assert.Contains(result.Reasons, x => x.Contains("maximum eligible amount", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Member_Below_Minimum_Membership_Is_Rejected()
    {
        var result = LoanEligibilityEvaluator.Evaluate(new LoanEligibilityInput(
            new DateTime(2026, 5, 15), new DateTime(2026, 9, 15), true,
            100_000m, 100_000m, 0, 6, 3m, 1));

        Assert.False(result.IsEligible);
        Assert.Contains(result.Reasons, x => x.Contains("Minimum membership", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Maximum_Active_Loan_Rule_Is_Enforced()
    {
        var result = LoanEligibilityEvaluator.Evaluate(new LoanEligibilityInput(
            new DateTime(2024, 1, 1), new DateTime(2026, 9, 1), true,
            100_000m, 100_000m, 1, 6, 3m, 1));

        Assert.False(result.IsEligible);
        Assert.Contains(result.Reasons, x => x.Contains("maximum allowed active loans", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Inactive_Member_Is_Rejected()
    {
        var result = LoanEligibilityEvaluator.Evaluate(new LoanEligibilityInput(
            new DateTime(2024, 1, 1), new DateTime(2026, 9, 1), false,
            100_000m, 100_000m, 0, 6, 3m, 1));

        Assert.False(result.IsEligible);
        Assert.Contains(result.Reasons, x => x.Contains("not active", StringComparison.OrdinalIgnoreCase));
    }
}
