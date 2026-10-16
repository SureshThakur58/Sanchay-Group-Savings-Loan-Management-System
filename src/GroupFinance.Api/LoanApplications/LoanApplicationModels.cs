namespace GroupFinance.Api.LoanApplications;

public sealed record CreateLoanApplicationRequest(
    long GroupId,
    long MemberId,
    DateTime ApplicationDate,
    decimal RequestedAmount,
    string Purpose,
    int? RequestedDurationMonths = null,
    decimal? ProposedInterestRate = null,
    string? ProposedInterestMethod = null);

public sealed record UpdateLoanApplicationRequest(
    DateTime ApplicationDate,
    decimal RequestedAmount,
    string Purpose,
    int RequestedDurationMonths,
    decimal ProposedInterestRate,
    string ProposedInterestMethod);

public sealed record AddLoanApplicationGuarantorRequest(long MemberId);

public sealed record LoanApplicationGuarantorResponse(
    long Id,
    long LoanApplicationId,
    long MemberId,
    string MemberNumber,
    string MemberName,
    string Status,
    DateTime ProvidedAt,
    DateTime CreatedAtUtc,
    long? CreatedByUserId);

public sealed record LoanEligibilityResponse(
    bool IsEligible,
    int MembershipMonths,
    decimal TotalContribution,
    decimal MaximumEligibleAmount,
    int ActiveLoanCount,
    int MaximumActiveLoans,
    int MinimumMembershipMonths,
    decimal LoanContributionMultiplier,
    IReadOnlyList<string> Reasons);

public sealed record LoanApplicationResponse(
    long Id,
    long GroupId,
    string GroupCode,
    string GroupName,
    long MemberId,
    string MemberNumber,
    string MemberName,
    string LoanApplicationNumber,
    DateTime ApplicationDate,
    decimal RequestedAmount,
    string Purpose,
    decimal ProposedInterestRate,
    string ProposedInterestMethod,
    int RequestedDurationMonths,
    decimal RequestedEmi,
    string Status,
    DateTime? DecisionDate,
    long? DecidedByUserId,
    string? DecisionRemarks,
    IReadOnlyCollection<LoanApplicationGuarantorResponse> Guarantors,
    LoanEligibilityResponse Eligibility,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId);

public sealed record LoanApplicationSummaryResponse(
    long Id,
    long GroupId,
    long MemberId,
    string MemberNumber,
    string MemberName,
    string LoanApplicationNumber,
    DateTime ApplicationDate,
    decimal RequestedAmount,
    decimal ProposedInterestRate,
    string ProposedInterestMethod,
    int RequestedDurationMonths,
    decimal RequestedEmi,
    string Status,
    int GuarantorCount,
    DateTime? DecisionDate);
