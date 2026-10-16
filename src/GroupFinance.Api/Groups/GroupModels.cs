namespace GroupFinance.Api.Groups;

public sealed record CreateGroupRequest(
    string Code,
    string Name,
    string? Description,
    DateTime FormationDate,
    string? RegistrationNumber,
    string CurrencyCode,
    GroupSettingsRequest Settings);

public sealed record UpdateGroupRequest(
    string Code,
    string Name,
    string? Description,
    DateTime FormationDate,
    string? RegistrationNumber,
    string CurrencyCode,
    bool IsActive);

public sealed record GroupSettingsRequest(
    decimal DefaultContributionAmount,
    string ContributionFrequency,
    decimal DefaultLoanInterestRate,
    string LoanInterestMethod,
    int DefaultLoanDuration,
    string LoanDurationUnit,
    string LoanInstallmentFrequency,
    decimal ContributionLateFeeRate,
    decimal ProfitReserveRate,
    string ProfitDistributionMethod,
    decimal LoanContributionMultiplier,
    int MinimumMembershipMonthsForLoan,
    int MaximumActiveLoansPerMember,
    int RequiredGuarantors,
    int MonetaryPrecision,
    string DateFormat,
    bool AllowBackdatedTransactions,
    bool AllowFutureFinancialPosting);

public sealed record GroupResponse(
    long Id,
    string Code,
    string Name,
    string? Description,
    DateTime FormationDate,
    string? RegistrationNumber,
    string CurrencyCode,
    bool IsActive,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId,
    GroupSettingsResponse? Settings);

public sealed record GroupSettingsResponse(
    long Id,
    long GroupId,
    decimal DefaultContributionAmount,
    string ContributionFrequency,
    decimal DefaultLoanInterestRate,
    string LoanInterestMethod,
    int DefaultLoanDuration,
    string LoanDurationUnit,
    string LoanInstallmentFrequency,
    decimal ContributionLateFeeRate,
    decimal ProfitReserveRate,
    string ProfitDistributionMethod,
    decimal LoanContributionMultiplier,
    int MinimumMembershipMonthsForLoan,
    int MaximumActiveLoansPerMember,
    int RequiredGuarantors,
    int MonetaryPrecision,
    string DateFormat,
    bool AllowBackdatedTransactions,
    bool AllowFutureFinancialPosting,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId);
