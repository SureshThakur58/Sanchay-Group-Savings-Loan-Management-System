namespace GroupFinance.Api.Audit;

public sealed record AuditLogResponse(
    long Id,
    long? UserId,
    string UserName,
    string EntityName,
    long? EntityId,
    string Action,
    string? OldValuesJson,
    string? NewValuesJson,
    string? Reason,
    string? IpAddress,
    DateTime CreatedAtUtc);

public sealed record ReverseFinancialTransactionRequest(
    DateTime? ReversalDate,
    string? Reason,
    string? ReferenceNumber);
