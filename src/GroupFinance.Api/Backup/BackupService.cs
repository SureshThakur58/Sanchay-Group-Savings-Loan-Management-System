using System.Text.Json.Serialization;
using System.Text.Json;
using GroupFinance.Core.Entities;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;

namespace GroupFinance.Api.Backup;

public sealed class BackupOptions
{
    public bool Enabled { get; set; }
    public string BackupDirectory { get; set; } = "D:/GroupFinance/Backups";
    public int RetentionDays { get; set; } = 30;
    public string TimeZoneId { get; set; } = "Nepal Standard Time";
    public string RecoveryDatabaseName { get; set; } = "GroupFinance_Phase29_RestoreTest";
    public string RecoveryDataDirectory { get; set; } = "D:/GroupFinance/Staging/Recovery";
    public string RecoveryLogDirectory { get; set; } = "D:/GroupFinance/Staging/Recovery";
    public bool AllowRestoreDrill { get; set; }
    public int CommandTimeoutSeconds { get; set; } = 600;
    public bool SchedulerEnabled { get; set; } = true;
}

public sealed class BackupService(
    GroupFinanceDbContext db,
    IOptions<BackupOptions> options,
    ILogger<BackupService> logger)
{
    private const string ScheduleSettingKey = "Backup.Schedules";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    private BackupOptions Options => options.Value;

    public BackupConfigurationResponse GetConfiguration() =>
        new(Options.Enabled, Options.BackupDirectory, Options.RetentionDays, Options.TimeZoneId,
            Options.RecoveryDatabaseName, Options.AllowRestoreDrill);

    public async Task<IReadOnlyList<BackupFileResponse>> ListBackupsAsync(CancellationToken ct)
    {
        EnsureEnabled();
        Directory.CreateDirectory(Options.BackupDirectory);
        var files = Directory.EnumerateFiles(Options.BackupDirectory, "*.bak", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(x => x.CreationTimeUtc)
            .ToArray();

        var result = new List<BackupFileResponse>(files.Length);
        foreach (var file in files)
        {
            var verified = await VerifyBackupFileAsync(file.FullName, ct);
            result.Add(new BackupFileResponse(
                file.Name,
                file.Length,
                file.CreationTimeUtc,
                verified));
        }

        return result;
    }

    public async Task<BackupResultResponse> CreateBackupAsync(CancellationToken ct)
    {
        EnsureEnabled();
        await _operationLock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Options.BackupDirectory);
            var databaseName = await GetDatabaseNameAsync(ct);
            var fileName = $"{SanitizeFileName(databaseName)}_FULL_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
            var path = Path.Combine(Options.BackupDirectory, fileName);
            var sqlPath = EscapeSqlLiteral(path);

            await ExecuteSqlAsync(
                $"BACKUP DATABASE {QuoteIdentifier(databaseName)} TO DISK = N'{sqlPath}' WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;",
                ct);

            var verified = await VerifyBackupFileAsync(path, ct);
            if (!verified)
                throw new InvalidOperationException("SQL Server created the backup, but RESTORE VERIFYONLY failed.");

            var info = new FileInfo(path);
            logger.LogInformation("Full database backup created and verified: {BackupFile}", path);
            return new BackupResultResponse(true, "Backup created and verified successfully.", fileName,
                info.CreationTimeUtc, info.Length, true);
        }
        finally { _operationLock.Release(); }
    }

    public async Task<bool> VerifyBackupAsync(string fileName, CancellationToken ct)
    {
        EnsureEnabled();
        var path = GetBackupPath(fileName);
        return await VerifyBackupFileAsync(path, ct);
    }

    public async Task<RetentionResponse> ApplyRetentionAsync(CancellationToken ct)
    {
        EnsureEnabled();
        var retention = Math.Clamp(Options.RetentionDays, 1, 3650);
        Directory.CreateDirectory(Options.BackupDirectory);
        var cutoff = DateTime.UtcNow.AddDays(-retention);
        var files = Directory.EnumerateFiles(Options.BackupDirectory, "*.bak", SearchOption.TopDirectoryOnly)
            .Select(x => new FileInfo(x)).ToArray();
        var deleted = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            if (file.CreationTimeUtc < cutoff)
            {
                file.Delete();
                deleted++;
            }
        }
        logger.LogInformation("Backup retention applied. Evaluated={Evaluated}, Deleted={Deleted}, Retained={Retained}, RetentionDays={RetentionDays}",
            files.Length, deleted, files.Length - deleted, retention);
        return new RetentionResponse(retention, files.Length, deleted, files.Length - deleted);
    }

    public async Task<RestoreResponse> RestoreBackupAsync(string fileName, CancellationToken ct)
    {
        EnsureEnabled();
        if (!Options.AllowRestoreDrill)
            throw new InvalidOperationException("Restore drill is disabled in this environment.");
        if (!IsSafeDatabaseName(Options.RecoveryDatabaseName))
            throw new InvalidOperationException("Recovery database name is invalid.");
        var path = GetBackupPath(fileName);

        await _operationLock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Options.RecoveryDataDirectory);
            var backupPath = EscapeSqlLiteral(path);
            var target = QuoteIdentifier(Options.RecoveryDatabaseName);

            await VerifyBackupFileAsync(path, ct);

            var masterConnection = await GetMasterConnectionStringAsync(ct);
            await using var connection = new SqlConnection(masterConnection);
            await connection.OpenAsync(ct);

            var logicalFiles = await GetRestoreLogicalFilesAsync(connection, backupPath, ct);
            var moves = new List<string>();
            foreach (var file in logicalFiles)
            {
                var isLog = string.Equals(file.Type, "L", StringComparison.OrdinalIgnoreCase);
                var directory = isLog ? Options.RecoveryLogDirectory : Options.RecoveryDataDirectory;
                Directory.CreateDirectory(directory);
                var extension = isLog ? ".ldf" : ".mdf";
                var targetPath = Path.Combine(directory, $"{SanitizeFileName(Options.RecoveryDatabaseName)}_{SanitizeFileName(file.LogicalName)}{extension}");
                moves.Add($"MOVE N'{EscapeSqlLiteral(file.LogicalName)}' TO N'{EscapeSqlLiteral(targetPath)}'");
            }

            var sql = $"""
IF DB_ID(N'{EscapeSqlLiteral(Options.RecoveryDatabaseName)}') IS NOT NULL
BEGIN
    ALTER DATABASE {target} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END;
RESTORE DATABASE {target}
FROM DISK = N'{backupPath}'
WITH REPLACE, RECOVERY, {string.Join(", ", moves)}, STATS = 10;
ALTER DATABASE {target} SET MULTI_USER;
""";
            await ExecuteSqlAsync(connection, sql, ct);
            logger.LogWarning("Backup restored to isolated recovery database {DatabaseName} from {BackupFile}.",
                Options.RecoveryDatabaseName, fileName);
            return new RestoreResponse(true, "Backup restored successfully to the isolated recovery database.",
                fileName, Options.RecoveryDatabaseName, DateTime.UtcNow);
        }
        finally { _operationLock.Release(); }
    }

    public async Task<IReadOnlyList<BackupScheduleResponse>> ListSchedulesAsync(CancellationToken ct)
    {
        var schedules = await LoadSchedulesAsync(ct);
        return schedules.Select(x => ToResponse(x)).OrderBy(x => x.Name).ToArray();
    }

    private static void ValidateSchedule(SaveBackupScheduleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
            throw new InvalidOperationException("Schedule name is required and must be at most 100 characters.");
        if (!new[] { "Daily", "Weekly", "Monthly" }.Contains(request.Frequency, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Frequency must be Daily, Weekly or Monthly.");
        if (!TryParseScheduleTime(request.TimeText, out _))
            throw new InvalidOperationException("Time must use 24-hour HH:mm format (for example 16:30).");
        if (request.Frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase) &&
            (!request.DayOfWeek.HasValue || request.DayOfWeek.Value is < 0 or > 6))
            throw new InvalidOperationException("Weekly schedules require a day of week from 0 (Sunday) to 6 (Saturday).");
        if (request.Frequency.Equals("Monthly", StringComparison.OrdinalIgnoreCase) &&
            (!request.DayOfMonth.HasValue || request.DayOfMonth.Value is < 1 or > 31))
            throw new InvalidOperationException("Monthly schedules require a day of month from 1 to 31.");
    }

    public async Task<BackupScheduleResponse> SaveScheduleAsync(Guid? id, SaveBackupScheduleRequest request, CancellationToken ct)
    {
        ValidateSchedule(request);
        var schedules = await LoadSchedulesAsync(ct);
        var existing = id.HasValue ? schedules.FirstOrDefault(x => x.Id == id.Value) : null;
        if (id.HasValue && existing is null) throw new InvalidOperationException("Backup schedule was not found.");

        var normalizedTime = NormalizeScheduleTime(request.TimeText);

        if (existing is null)
        {
            existing = new BackupSchedule(
                Guid.NewGuid(), request.Name.Trim(), request.Frequency.Trim(), normalizedTime,
                request.DayOfWeek, request.DayOfMonth, request.Enabled, null);
            schedules.Add(existing);
        }
        else
        {
            existing.Name = request.Name.Trim();
            existing.Frequency = request.Frequency.Trim();
            existing.TimeText = normalizedTime;
            existing.DayOfWeek = request.DayOfWeek;
            existing.DayOfMonth = request.DayOfMonth;
            existing.Enabled = request.Enabled;
        }

        await SaveSchedulesAsync(schedules, ct);
        return ToResponse(existing);
    }

    public async Task DeleteScheduleAsync(Guid id, CancellationToken ct)
    {
        var schedules = await LoadSchedulesAsync(ct);
        if (schedules.RemoveAll(x => x.Id == id) == 0)
            throw new InvalidOperationException("Backup schedule was not found.");
        await SaveSchedulesAsync(schedules, ct);
    }

    public async Task RunDueSchedulesAsync(CancellationToken ct)
    {
        if (!Options.Enabled || !Options.SchedulerEnabled) return;
        var schedules = await LoadSchedulesAsync(ct);
        var nowUtc = DateTime.UtcNow;
        var zone = ResolveTimeZone(Options.TimeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
        var changed = false;

        foreach (var schedule in schedules.Where(x => x.Enabled))
        {
            if (!IsDue(schedule, local)) continue;
            try
            {
                await CreateBackupAsync(ct);
                schedule.LastRunUtc = nowUtc;
                changed = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled backup failed for schedule {ScheduleName}.", schedule.Name);
            }
        }

        if (changed) await SaveSchedulesAsync(schedules, ct);
    }

    private async Task<List<BackupSchedule>> LoadSchedulesAsync(CancellationToken ct)
    {
        var setting = await db.SystemSettings.SingleOrDefaultAsync(x => x.SettingKey == ScheduleSettingKey, ct);
        if (setting is null) return [];
        try { return JsonSerializer.Deserialize<List<BackupSchedule>>(setting.SettingValue, JsonOptions) ?? []; }
        catch (JsonException ex) { logger.LogError(ex, "Invalid Backup.Schedules system setting."); return []; }
    }

    private async Task SaveSchedulesAsync(List<BackupSchedule> schedules, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(schedules, JsonOptions);
        var setting = await db.SystemSettings.SingleOrDefaultAsync(x => x.SettingKey == ScheduleSettingKey, ct);
        if (setting is null)
        {
            setting = new SystemSetting { SettingKey = ScheduleSettingKey, SettingValue = json, Description = "UI-managed database backup schedules.", IsSensitive = false };
            db.SystemSettings.Add(setting);
        }
        else { setting.SettingValue = json; setting.ModifiedAtUtc = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> VerifyBackupFileAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Backup file was not found.", path);
        await using var connection = await OpenConnectionAsync(ct);
        var command = connection.CreateCommand();
        command.CommandTimeout = Options.CommandTimeoutSeconds;
        command.CommandText = $"RESTORE VERIFYONLY FROM DISK = N'{EscapeSqlLiteral(path)}' WITH CHECKSUM;";
        await command.ExecuteNonQueryAsync(ct);
        return true;
    }

    private async Task<string> GetDatabaseNameAsync(CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_NAME();";
        return Convert.ToString(await command.ExecuteScalarAsync(ct)) ?? throw new InvalidOperationException("Could not determine database name.");
    }

    private async Task<string> GetMasterConnectionStringAsync(CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        var builder = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" };
        return builder.ConnectionString;
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("SQL Server connection string is not configured.");
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private async Task ExecuteSqlAsync(string sql, CancellationToken ct)
    {
        await using var connection = new SqlConnection(await GetMasterConnectionStringAsync(ct));
        await connection.OpenAsync(ct);
        await ExecuteSqlAsync(connection, sql, ct);
    }

    private async Task ExecuteSqlAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = Options.CommandTimeoutSeconds;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<LogicalFile>> GetRestoreLogicalFilesAsync(SqlConnection connection, string backupPath, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 600;
        command.CommandText = $"RESTORE FILELISTONLY FROM DISK = N'{backupPath}';";
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<LogicalFile>();
        while (await reader.ReadAsync(ct))
            result.Add(new LogicalFile(reader["LogicalName"].ToString()!, reader["Type"].ToString()!));
        return result;
    }

    private string GetBackupPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            throw new InvalidOperationException("Invalid backup file name.");

        // Swagger/manual callers sometimes omit the .bak suffix. Normalize it safely
        // while still rejecting paths and keeping access inside BackupDirectory.
        if (!fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            fileName += ".bak";

        var path = Path.GetFullPath(Path.Combine(Options.BackupDirectory, fileName));
        var root = Path.GetFullPath(Options.BackupDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backup file is outside the configured backup directory.");
        return path;
    }

    private void EnsureEnabled()
    {
        if (!Options.Enabled) throw new InvalidOperationException("Database backup is disabled in this environment.");
    }

    private static string QuoteIdentifier(string value) => "[" + value.Replace("]", "]]") + "]";
    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");
    private static string SanitizeFileName(string value) => string.Concat(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
    private static bool IsSafeDatabaseName(string value) => RegexLikeIdentifier(value);

    private static bool RegexLikeIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value.All(c => char.IsLetterOrDigit(c) || c == '_');

    private BackupScheduleResponse ToResponse(BackupSchedule s)
    {
        var zone = ResolveTimeZone(Options.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        var nextLocal = CalculateNextOccurrence(s, nowLocal);
        var nextUtc = TimeZoneInfo.ConvertTimeToUtc(nextLocal, zone);
        return new BackupScheduleResponse(
            s.Id, s.Name, s.Frequency, s.TimeText, s.DayOfWeek, s.DayOfMonth,
            s.Enabled, s.LastRunUtc, nextUtc);
    }

    private static DateTime CalculateNextOccurrence(BackupSchedule s, DateTime nowLocal)
    {
        if (!TryParseScheduleTime(s.TimeText, out var time))
            return nowLocal.AddDays(1);

        var candidate = nowLocal.Date.Add(time);
        for (var i = 0; i <= 370; i++)
        {
            var date = nowLocal.Date.AddDays(i);
            var matches = s.Frequency switch
            {
                "Daily" => true,
                "Weekly" => s.DayOfWeek.HasValue && (int)date.DayOfWeek == s.DayOfWeek.Value,
                "Monthly" => s.DayOfMonth.HasValue && date.Day == s.DayOfMonth.Value,
                _ => false
            };
            if (matches)
            {
                candidate = date.Add(time);
                if (candidate > nowLocal) return candidate;
            }
        }
        return nowLocal.AddDays(1);
    }

    private static bool TryParseScheduleTime(string? value, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();

        // Canonical storage format is 24-hour HH:mm.
        if (TimeSpan.TryParseExact(
                text,
                new[] { @"hh\:mm", @"h\:mm" },
                System.Globalization.CultureInfo.InvariantCulture,
                out time))
        {
            return time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
        }

        // Accept localized/12-hour input as a compatibility measure, then normalize it.
        if (DateTime.TryParseExact(
                text,
                new[] { "h:mm tt", "hh:mm tt" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            time = parsed.TimeOfDay;
            return true;
        }

        return false;
    }

    private static string NormalizeScheduleTime(string value)
    {
        if (!TryParseScheduleTime(value, out var time))
            throw new InvalidOperationException("Time must use 24-hour HH:mm format (for example 16:30).");

        return time.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Utc; }
    }

    private bool IsDue(BackupSchedule s, DateTime local)
    {
        if (!TryParseScheduleTime(s.TimeText, out var time)) return false;
        if (local.Hour != time.Hours || local.Minute != time.Minutes) return false;
        if (s.LastRunUtc.HasValue)
        {
            var lastLocal = TimeZoneInfo.ConvertTimeFromUtc(s.LastRunUtc.Value, ResolveTimeZone(Options.TimeZoneId));
            if (lastLocal.Year == local.Year && lastLocal.Month == local.Month &&
                lastLocal.Day == local.Day && lastLocal.Hour == local.Hour && lastLocal.Minute == local.Minute)
                return false;
        }
        return s.Frequency switch
        {
            "Daily" => true,
            "Weekly" => s.DayOfWeek.HasValue && (int)local.DayOfWeek == s.DayOfWeek.Value,
            "Monthly" => s.DayOfMonth.HasValue && local.Day == s.DayOfMonth.Value,
            _ => false
        };
    }

    private sealed class BackupSchedule
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Frequency { get; set; } = "Daily";
        [JsonPropertyName("Time")]
        public string TimeText { get; set; } = "02:00";
        public int? DayOfWeek { get; set; }
        public int? DayOfMonth { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTime? LastRunUtc { get; set; }
        public BackupSchedule() { }
        public BackupSchedule(Guid id, string name, string frequency, string time, int? dayOfWeek, int? dayOfMonth, bool enabled, DateTime? lastRunUtc)
        { Id=id; Name=name; Frequency=frequency; TimeText=time; DayOfWeek=dayOfWeek; DayOfMonth=dayOfMonth; Enabled=enabled; LastRunUtc=lastRunUtc; }
    }
    private sealed record LogicalFile(string LogicalName, string Type);
}
