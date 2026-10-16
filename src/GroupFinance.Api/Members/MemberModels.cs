namespace GroupFinance.Api.Members;

public sealed record CreateMemberRequest(
    long GroupId,
    string MemberNumber,
    string FullName,
    string? Address,
    string? MobileNumber,
    string? Email,
    DateTime? DateOfBirth,
    DateTime JoinDate,
    string? CitizenshipNumber,
    decimal? ContributionAmountOverride,
    string? Notes,
    IReadOnlyCollection<CreateMemberNomineeRequest>? Nominees);

public sealed record UpdateMemberRequest(
    string MemberNumber,
    string FullName,
    string? Address,
    string? MobileNumber,
    string? Email,
    DateTime? DateOfBirth,
    DateTime JoinDate,
    DateTime? ExitDate,
    bool IsActive,
    string? CitizenshipNumber,
    decimal? ContributionAmountOverride,
    string? Notes);

public sealed record CreateMemberNomineeRequest(
    string FullName,
    string? Relationship,
    string? MobileNumber,
    string? Address,
    decimal? SharePercentage,
    bool IsActive = true);

public sealed record UpdateMemberNomineeRequest(
    string FullName,
    string? Relationship,
    string? MobileNumber,
    string? Address,
    decimal? SharePercentage,
    bool IsActive);

public sealed record MemberNomineeResponse(
    long Id,
    long MemberId,
    string FullName,
    string? Relationship,
    string? MobileNumber,
    string? Address,
    decimal? SharePercentage,
    bool IsActive,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId);

public sealed record MemberResponse(
    long Id,
    long GroupId,
    string GroupCode,
    string GroupName,
    string MemberNumber,
    string FullName,
    string? Address,
    string? MobileNumber,
    string? Email,
    DateTime? DateOfBirth,
    DateTime JoinDate,
    DateTime? ExitDate,
    bool IsActive,
    string? CitizenshipNumber,
    decimal? ContributionAmountOverride,
    string? Notes,
    IReadOnlyCollection<MemberNomineeResponse> Nominees,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId);
