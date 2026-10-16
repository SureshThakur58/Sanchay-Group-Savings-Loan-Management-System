namespace GroupFinance.Api.ProfitDistributions;

public static class ProfitDistributionMethods
{
    public const string Equal = "Equal";
    public const string ContributionBased = "ContributionBased";
    public const string Weighted = "Weighted";
    public static readonly string[] All = [Equal, ContributionBased, Weighted];
}

public sealed record ProfitDistributionPreviewRequest(
    long GroupId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string DistributionMethod);

public sealed record ProfitDistributionRequest(
    long GroupId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string DistributionMethod,
    string? Description = null,
    string? ReferenceNumber = null);

public sealed record ProfitDistributionMemberResponse(
    long MemberId,
    string MemberNumber,
    string MemberName,
    decimal ContributionAmount,
    decimal MembershipMonths,
    decimal AverageBalance,
    decimal EligibilityFactor,
    decimal CalculatedShare,
    decimal DistributedAmount);

public sealed record ProfitDistributionResponse(
    long Id,
    long GroupId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal NetProfit,
    decimal ReserveAmount,
    decimal DistributableAmount,
    string DistributionMethod,
    string Status,
    DateTime? DistributedDate,
    IReadOnlyCollection<ProfitDistributionMemberResponse> Details,
    long? FinancialTransactionId);
