namespace GroupFinance.Api.FileStorage;

public sealed record AttachmentResponse(
    long Id,
    string EntityName,
    long EntityId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DateTime CreatedAtUtc,
    long? CreatedByUserId);

public sealed record AttachmentDownload(
    string FileName,
    string ContentType,
    long Length,
    Stream Content);
