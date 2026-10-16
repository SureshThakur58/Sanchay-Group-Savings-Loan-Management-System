namespace GroupFinance.Api.Contributions;

public sealed record CreateContributionPlanRequest(
    long GroupId,
    string Name,
    decimal Amount,
    string Frequency,
    int IntervalMonths,
    int DueDay,
    decimal LateFeeRate,
    bool IsActive = true);

public sealed record UpdateContributionPlanRequest(
    string Name,
    decimal Amount,
    string Frequency,
    int IntervalMonths,
    int DueDay,
    decimal LateFeeRate,
    bool IsActive);

public sealed record ContributionPlanResponse(
    long Id,
    long GroupId,
    string GroupCode,
    string GroupName,
    string Name,
    decimal Amount,
    string Frequency,
    int IntervalMonths,
    int DueDay,
    decimal LateFeeRate,
    bool IsActive,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId);

public sealed record GenerateContributionSchedulesRequest(
    long ContributionPlanId,
    DateTime FromDate,
    DateTime ToDate);

public sealed record ContributionScheduleResponse(
    long Id,
    long ContributionPlanId,
    string PlanName,
    long MemberId,
    string MemberNumber,
    string MemberName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime DueDate,
    decimal DueAmount,
    decimal PaidAmount,
    decimal LateFeeAmount,
    decimal WaivedLateFeeAmount,
    decimal TotalDue,
    decimal OutstandingAmount,
    string Status,
    IReadOnlyCollection<ContributionPaymentResponse> Payments);

public sealed record ContributionPaymentResponse(
    long Id,
    long ContributionScheduleId,
    long MemberId,
    DateTime TransactionDate,
    decimal Amount,
    string PaymentMethod,
    long? FinancialAccountId,
    string? ReferenceNumber,
    string? Description,
    string Status,
    long? FinancialTransactionId,
    DateTime CreatedAtUtc,
    long? CreatedByUserId);

public sealed record CreateContributionPaymentRequest(
    DateTime TransactionDate,
    decimal Amount,
    string PaymentMethod,
    long FinancialAccountId,
    string? ReferenceNumber,
    string? Description);

public sealed record WaiveContributionLateFeeRequest(
    decimal? Amount,
    string? Reason);

public sealed record ContributionScheduleGenerationResponse(
    long ContributionPlanId,
    DateTime FromDate,
    DateTime ToDate,
    int SchedulesCreated,
    int SchedulesAlreadyExisting);
