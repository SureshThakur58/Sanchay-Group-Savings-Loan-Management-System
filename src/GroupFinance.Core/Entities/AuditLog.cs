namespace GroupFinance.Core.Entities;

public sealed class AuditLog : BaseEntity
{
    public long? UserId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
}
