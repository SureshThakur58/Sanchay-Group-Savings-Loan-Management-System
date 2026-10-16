using System.Text.Json.Serialization;

namespace GroupFinance.Api.Backup;

public sealed record BackupFileResponse(
    string FileName,
    long SizeBytes,
    DateTime CreatedAtUtc,
    bool Verified);

public sealed record BackupResultResponse(
    bool Success,
    string Message,
    string? FileName,
    DateTime? CreatedAtUtc,
    long? SizeBytes,
    bool Verified);

public sealed record BackupScheduleResponse(
    Guid Id,
    string Name,
    string Frequency,
    [property: JsonPropertyName("Time")] string TimeText,
    int? DayOfWeek,
    int? DayOfMonth,
    bool Enabled,
    DateTime? LastRunUtc,
    DateTime? NextRunUtc);

public sealed record SaveBackupScheduleRequest(
    string Name,
    string Frequency,
    [property: JsonPropertyName("Time")] string TimeText,
    int? DayOfWeek,
    int? DayOfMonth,
    bool Enabled);

public sealed record RetentionResponse(
    int RetentionDays,
    int Evaluated,
    int Deleted,
    int Retained);

public sealed record RestoreResponse(
    bool Success,
    string Message,
    string SourceBackup,
    string TargetDatabase,
    DateTime CompletedAtUtc);

public sealed record BackupConfigurationResponse(
    bool Enabled,
    string BackupDirectory,
    int RetentionDays,
    string TimeZoneId,
    string RecoveryDatabaseName,
    bool AllowRestoreDrill);
