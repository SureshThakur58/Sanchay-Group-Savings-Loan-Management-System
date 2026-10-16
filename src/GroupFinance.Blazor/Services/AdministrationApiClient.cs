using System.Text.Json.Serialization;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed class CreateUserModel
{
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Password { get; set; } = string.Empty;
    public string RoleName { get; set; } = "GroupViewer";
}

public sealed class UpdateUserModel
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string RoleName { get; set; } = "GroupViewer";
    public bool IsActive { get; set; } = true;
}

public sealed class ResetPasswordModel { public string NewPassword { get; set; } = string.Empty; }
public sealed class UpdateRoleModel { public string Description { get; set; } = string.Empty; }
public sealed class UpdateRolePermissionsModel { public List<string> PermissionCodes { get; set; } = []; }
public sealed class UpdateSystemSettingModel { public string SettingValue { get; set; } = string.Empty; }

public sealed record UserAdminModel(long Id, string UserName, string FullName, string? Email, bool IsActive, bool MustChangePassword, int FailedLoginCount, DateTime? LockedUntilUtc, DateTime? LastLoginAtUtc, DateTime? PasswordChangedAtUtc, IReadOnlyList<string> Roles, DateTime CreatedAtUtc, long? CreatedByUserId, DateTime? ModifiedAtUtc, long? ModifiedByUserId);
public sealed record RoleAdminModel(long Id, string Name, string Description, IReadOnlyList<string> PermissionCodes, DateTime CreatedAtUtc, DateTime? ModifiedAtUtc);
public sealed record PermissionAdminModel(long Id, string Code, string Name, string Module);
public sealed record SystemSettingAdminModel(long Id, string SettingKey, string SettingValue, string? Description, bool IsSensitive, DateTime CreatedAtUtc, DateTime? ModifiedAtUtc);

public sealed record BackupFileModel(string FileName, long SizeBytes, DateTime CreatedAtUtc, bool Verified);
public sealed record BackupResultModel(bool Success, string Message, string? FileName, DateTime? CreatedAtUtc, long? SizeBytes, bool Verified);
public sealed record BackupScheduleModel(Guid Id, string Name, string Frequency, [property: JsonPropertyName("Time")] string TimeText, int? DayOfWeek, int? DayOfMonth, bool Enabled, DateTime? LastRunUtc, DateTime? NextRunUtc);
public sealed record SaveBackupScheduleModel(string Name, string Frequency, [property: JsonPropertyName("Time")] string TimeText, int? DayOfWeek, int? DayOfMonth, bool Enabled);
public sealed record RetentionModel(int RetentionDays, int Evaluated, int Deleted, int Retained);
public sealed record RestoreModel(bool Success, string Message, string SourceBackup, string TargetDatabase, DateTime CompletedAtUtc);
public sealed record BackupConfigurationModel(bool Enabled, string BackupDirectory, int RetentionDays, string TimeZoneId, string RecoveryDatabaseName, bool AllowRestoreDrill);

public sealed class AdministrationApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, IReadOnlyList<UserAdminModel>? Data)> GetUsersAsync()
    {
        var result = await GetAsync<List<UserAdminModel>>("api/administration/users");
        return (result.Success, result.Error, result.Data);
    }

    public async Task<(bool Success, string? Error, IReadOnlyList<RoleAdminModel>? Data)> GetRolesAsync()
    {
        var result = await GetAsync<List<RoleAdminModel>>("api/administration/roles");
        return (result.Success, result.Error, result.Data);
    }

    public async Task<(bool Success, string? Error, IReadOnlyList<PermissionAdminModel>? Data)> GetPermissionsAsync()
    {
        var result = await GetAsync<List<PermissionAdminModel>>("api/administration/permissions");
        return (result.Success, result.Error, result.Data);
    }

    public async Task<(bool Success, string? Error, IReadOnlyList<SystemSettingAdminModel>? Data)> GetSystemSettingsAsync()
    {
        var result = await GetAsync<List<SystemSettingAdminModel>>("api/administration/system-settings");
        return (result.Success, result.Error, result.Data);
    }

    public Task<(bool Success, string? Error, UserAdminModel? Data)> CreateUserAsync(CreateUserModel model) => SendAsync<UserAdminModel>(HttpMethod.Post, "api/administration/users", model);
    public Task<(bool Success, string? Error, UserAdminModel? Data)> UpdateUserAsync(long id, UpdateUserModel model) => SendAsync<UserAdminModel>(HttpMethod.Put, $"api/administration/users/{id}", model);
    public async Task<(bool Success, string? Error)> ResetPasswordAsync(long id, ResetPasswordModel model) => await SendNoContentAsync(HttpMethod.Post, $"api/administration/users/{id}/reset-password", model);
    public Task<(bool Success, string? Error, RoleAdminModel? Data)> UpdateRoleAsync(long id, UpdateRoleModel model) => SendAsync<RoleAdminModel>(HttpMethod.Put, $"api/administration/roles/{id}", model);
    public Task<(bool Success, string? Error, RoleAdminModel? Data)> UpdateRolePermissionsAsync(long id, UpdateRolePermissionsModel model) => SendAsync<RoleAdminModel>(HttpMethod.Put, $"api/administration/roles/{id}/permissions", model);
    public Task<(bool Success, string? Error, SystemSettingAdminModel? Data)> UpdateSystemSettingAsync(long id, UpdateSystemSettingModel model) => SendAsync<SystemSettingAdminModel>(HttpMethod.Put, $"api/administration/system-settings/{id}", model);

    public Task<(bool Success, string? Error, BackupConfigurationModel? Data)> GetBackupConfigurationAsync()
        => GetAsync<BackupConfigurationModel>("api/backup/configuration");

    public async Task<(bool Success, string? Error, IReadOnlyList<BackupFileModel>? Data)> GetBackupsAsync()
    {
        var result = await GetAsync<List<BackupFileModel>>("api/backup/files");
        return (result.Success, result.Error, result.Data);
    }

    public Task<(bool Success, string? Error, BackupResultModel? Data)> CreateBackupAsync()
        => SendAsync<BackupResultModel>(HttpMethod.Post, "api/backup/create", new { });

    public Task<(bool Success, string? Error, RetentionModel? Data)> ApplyBackupRetentionAsync()
        => SendAsync<RetentionModel>(HttpMethod.Post, "api/backup/retention", new { });

    public Task<(bool Success, string? Error)> VerifyBackupAsync(string fileName)
        => SendNoContentAsync(HttpMethod.Post, "api/backup/verify", fileName);

    public async Task<(bool Success, string? Error, IReadOnlyList<BackupScheduleModel>? Data)> GetBackupSchedulesAsync()
    {
        var result = await GetAsync<List<BackupScheduleModel>>("api/backup/schedules");
        return (result.Success, result.Error, result.Data);
    }

    public Task<(bool Success, string? Error, BackupScheduleModel? Data)> CreateBackupScheduleAsync(SaveBackupScheduleModel model)
        => SendAsync<BackupScheduleModel>(HttpMethod.Post, "api/backup/schedules", model);

    public Task<(bool Success, string? Error, BackupScheduleModel? Data)> UpdateBackupScheduleAsync(Guid id, SaveBackupScheduleModel model)
        => SendAsync<BackupScheduleModel>(HttpMethod.Put, $"api/backup/schedules/{id}", model);

    public Task<(bool Success, string? Error)> DeleteBackupScheduleAsync(Guid id)
        => SendNoContentAsync(HttpMethod.Delete, $"api/backup/schedules/{id}", null);

    public Task<(bool Success, string? Error, RestoreModel? Data)> RestoreBackupAsync(string fileName)
        => SendAsync<RestoreModel>(HttpMethod.Post, "api/backup/restore", fileName);

    private async Task<(bool Success, string? Error, T? Data)> GetAsync<T>(string uri)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, uri);
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), default);
            return (true, null, await response.Content.ReadFromJsonAsync<T>());
        }
        catch (Exception ex) { return (false, ex.Message, default); }
    }

    private async Task<(bool Success, string? Error, T? Data)> SendAsync<T>(HttpMethod method, string uri, object body)
    {
        try
        {
            using var request = CreateRequest(method, uri);
            request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response), default);
            return (true, null, await response.Content.ReadFromJsonAsync<T>());
        }
        catch (Exception ex) { return (false, ex.Message, default); }
    }

    private async Task<(bool Success, string? Error)> SendNoContentAsync(HttpMethod method, string uri, object? body)
    {
        try
        {
            using var request = CreateRequest(method, uri);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(authState.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
        return request;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try { var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(); return error?.Message ?? $"Request failed with HTTP {(int)response.StatusCode}."; }
        catch { return $"Request failed with HTTP {(int)response.StatusCode}."; }
    }

    private sealed record ErrorResponse(string Message);
}
