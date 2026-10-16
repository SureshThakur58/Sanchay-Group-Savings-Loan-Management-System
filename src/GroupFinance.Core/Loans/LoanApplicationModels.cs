namespace GroupFinance.Core.Loans;

public static class LoanApplicationStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Disbursed = "Disbursed";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Draft, Submitted, Approved, Rejected, Disbursed, Closed, Cancelled];
}

public sealed record LoanEligibilityInput(
    DateTime JoinDate,
    DateTime ApplicationDate,
    bool IsActiveMember,
    decimal TotalContribution,
    decimal RequestedAmount,
    int ActiveLoanCount,
    int MinimumMembershipMonths,
    decimal LoanContributionMultiplier,
    int MaximumActiveLoans);

public sealed record LoanEligibilityResult(
    bool IsEligible,
    int MembershipMonths,
    decimal MaximumEligibleAmount,
    int ActiveLoanCount,
    IReadOnlyList<string> Reasons);

public static class LoanEligibilityEvaluator
{
    public static LoanEligibilityResult Evaluate(LoanEligibilityInput input)
    {
        var reasons = new List<string>();
        var applicationDate = input.ApplicationDate.Date;
        var membershipMonths = CalculateCompletedMonths(input.JoinDate.Date, applicationDate);
        var maximumAmount = Math.Max(0m, input.TotalContribution * input.LoanContributionMultiplier);

        if (!input.IsActiveMember) reasons.Add("The member is not active.");
        if (membershipMonths < input.MinimumMembershipMonths) reasons.Add($"Minimum membership period is {input.MinimumMembershipMonths} months.");
        if (input.RequestedAmount > maximumAmount) reasons.Add($"Requested amount exceeds the maximum eligible amount of {maximumAmount:N2}.");
        if (input.ActiveLoanCount >= input.MaximumActiveLoans) reasons.Add($"The member already has the maximum allowed active loans ({input.MaximumActiveLoans}).");
        if (input.RequestedAmount <= 0m) reasons.Add("Requested amount must be greater than zero.");

        return new LoanEligibilityResult(reasons.Count == 0, membershipMonths, maximumAmount, input.ActiveLoanCount, reasons);
    }

    private static int CalculateCompletedMonths(DateTime start, DateTime end)
    {
        if (end < start) return 0;
        var months = (end.Year - start.Year) * 12 + end.Month - start.Month;
        if (end.Day < start.Day) months--;
        return Math.Max(0, months);
    }
}
