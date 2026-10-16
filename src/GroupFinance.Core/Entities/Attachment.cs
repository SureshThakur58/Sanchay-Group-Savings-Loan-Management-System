namespace GroupFinance.Core.Entities;

public sealed class Attachment : BaseEntity
{
    public string EntityName { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
}
