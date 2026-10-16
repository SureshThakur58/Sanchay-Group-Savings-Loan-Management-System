namespace GroupFinance.Api.Administration;

public sealed record UserResponse(
    long Id,
    string UserName,
    string FullName,
    string? Email,
    bool IsActive,
    bool MustChangePassword,
    int FailedLoginCount,
    DateTime? LockedUntilUtc,
    DateTime? LastLoginAtUtc,
    DateTime? PasswordChangedAtUtc,
    IReadOnlyList<string> Roles,
    DateTime CreatedAtUtc,
    long? CreatedByUserId,
    DateTime? ModifiedAtUtc,
    long? ModifiedByUserId);

public sealed record CreateUserRequest(
    string UserName,
    string FullName,
    string? Email,
    string Password,
    string RoleName);

public sealed record UpdateUserRequest(
    string FullName,
    string? Email,
    string RoleName,
    bool IsActive);

public sealed record ResetUserPasswordRequest(string NewPassword);

public sealed record RoleResponse(
    long Id,
    string Name,
    string Description,
    IReadOnlyList<string> PermissionCodes,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record UpdateRoleRequest(string Description);
public sealed record UpdateRolePermissionsRequest(IReadOnlyList<string> PermissionCodes);

public sealed record PermissionResponse(long Id, string Code, string Name, string Module);

public sealed record SystemSettingResponse(
    long Id,
    string SettingKey,
    string SettingValue,
    string? Description,
    bool IsSensitive,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record UpdateSystemSettingRequest(string SettingValue);
