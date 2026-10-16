using GroupFinance.Core.Entities;
using GroupFinance.Core.Security;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Administration;

public sealed class AdministrationValidationException(string message) : Exception(message);

public sealed class AdministrationService(GroupFinanceDbContext db)
{
    private static readonly PasswordHasher<ApplicationUser> PasswordHasher = new();

    public async Task<IReadOnlyList<UserResponse>> ListUsersAsync(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .OrderBy(x => x.UserName)
            .ToListAsync(ct);
        return users.Select(MapUser).ToArray();
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, long currentUserId, CancellationToken ct)
    {
        ValidateUserName(request.UserName);
        ValidateFullName(request.FullName);
        ValidatePassword(request.Password);
        var role = await GetRoleAsync(request.RoleName, ct);
        await EnsureRoleCanBeAssignedAsync(role.Name, currentUserId, ct);

        var username = request.UserName.Trim();
        if (await db.Users.AnyAsync(x => x.UserName == username, ct))
            throw new AdministrationValidationException($"User name '{username}' already exists.");

        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            UserName = username,
            FullName = request.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            IsActive = true,
            MustChangePassword = true,
            CreatedAtUtc = now,
            CreatedByUserId = currentUserId
        };
        user.PasswordHash = PasswordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            CreatedAtUtc = now,
            CreatedByUserId = currentUserId
        });
        await db.SaveChangesAsync(ct);
        return await GetUserResponseAsync(user.Id, ct);
    }

    public async Task<UserResponse> UpdateUserAsync(long userId, UpdateUserRequest request, long currentUserId, CancellationToken ct)
    {
        if (userId <= 0) throw new AdministrationValidationException("User ID is required.");
        ValidateFullName(request.FullName);
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AdministrationValidationException("User was not found.");
        var role = await GetRoleAsync(request.RoleName, ct);
        await EnsureRoleCanBeAssignedAsync(role.Name, currentUserId, ct);

        if (user.Id == currentUserId && !request.IsActive)
            throw new AdministrationValidationException("You cannot deactivate your own user account.");

        var wasSuper = user.UserRoles.Any(x => x.Role.Name == RoleNames.SuperAdministrator);
        var becomesNonSuper = role.Name != RoleNames.SuperAdministrator;
        if (wasSuper && becomesNonSuper && await IsLastActiveSuperAdministratorAsync(user.Id, ct))
            throw new AdministrationValidationException("The last active Super Administrator cannot be removed.");

        if (!user.IsActive && !request.IsActive) { }
        var now = DateTime.UtcNow;
        user.FullName = request.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        user.IsActive = request.IsActive;
        user.ModifiedAtUtc = now;
        user.ModifiedByUserId = currentUserId;

        foreach (var existing in user.UserRoles.ToArray())
            db.UserRoles.Remove(existing);
        user.UserRoles.Clear();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, CreatedAtUtc = now, CreatedByUserId = currentUserId });
        await db.SaveChangesAsync(ct);
        return await GetUserResponseAsync(user.Id, ct);
    }

    public async Task ResetPasswordAsync(long userId, ResetUserPasswordRequest request, long currentUserId, CancellationToken ct)
    {
        ValidatePassword(request.NewPassword);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AdministrationValidationException("User was not found.");
        var now = DateTime.UtcNow;
        user.PasswordHash = PasswordHasher.HashPassword(user, request.NewPassword);
        user.MustChangePassword = true;
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.PasswordChangedAtUtc = now;
        user.ModifiedAtUtc = now;
        user.ModifiedByUserId = currentUserId;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().Include(x => x.RolePermissions).ThenInclude(x => x.Permission)
            .OrderBy(x => x.Name).ToListAsync(ct);
        return roles.Select(MapRole).ToArray();
    }

    public async Task<RoleResponse> UpdateRoleAsync(long roleId, UpdateRoleRequest request, long currentUserId, CancellationToken ct)
    {
        var role = await db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleOrDefaultAsync(x => x.Id == roleId, ct)
            ?? throw new AdministrationValidationException("Role was not found.");
        if (string.IsNullOrWhiteSpace(request.Description)) throw new AdministrationValidationException("Role description is required.");
        if (request.Description.Trim().Length > 500) throw new AdministrationValidationException("Role description cannot exceed 500 characters.");
        role.Description = request.Description.Trim();
        role.ModifiedAtUtc = DateTime.UtcNow;
        role.ModifiedByUserId = currentUserId;
        await db.SaveChangesAsync(ct);
        return MapRole(role);
    }

    public async Task<RoleResponse> UpdateRolePermissionsAsync(long roleId, UpdateRolePermissionsRequest request, long currentUserId, CancellationToken ct)
    {
        var role = await db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleOrDefaultAsync(x => x.Id == roleId, ct)
            ?? throw new AdministrationValidationException("Role was not found.");
        var codes = request.PermissionCodes?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            ?? Array.Empty<string>();
        var permissions = await db.Permissions.Where(x => codes.Contains(x.Code)).ToListAsync(ct);
        if (permissions.Count != codes.Length)
        {
            var missing = codes.Except(permissions.Select(x => x.Code), StringComparer.OrdinalIgnoreCase);
            throw new AdministrationValidationException($"Unknown permission code(s): {string.Join(", ", missing)}.");
        }

        if (role.Name == RoleNames.SuperAdministrator)
        {
            var mandatory = new[] { PermissionCodes.UsersManage, PermissionCodes.RolesManage, PermissionCodes.SystemSettingsManage };
            if (mandatory.Any(x => !codes.Contains(x, StringComparer.OrdinalIgnoreCase)))
                throw new AdministrationValidationException("Super Administrator must retain Users.Manage, Roles.Manage and SystemSettings.Manage.");
        }

        foreach (var existing in role.RolePermissions.ToArray()) db.RolePermissions.Remove(existing);
        role.RolePermissions.Clear();
        var now = DateTime.UtcNow;
        foreach (var permission in permissions)
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, CreatedAtUtc = now, CreatedByUserId = currentUserId });
        role.ModifiedAtUtc = now;
        role.ModifiedByUserId = currentUserId;
        await db.SaveChangesAsync(ct);
        await db.Entry(role).Collection(x => x.RolePermissions).Query().Include(x => x.Permission).LoadAsync(ct);
        return MapRole(role);
    }

    public async Task<IReadOnlyList<PermissionResponse>> ListPermissionsAsync(CancellationToken ct)
    {
        return await db.Permissions.AsNoTracking().OrderBy(x => x.Module).ThenBy(x => x.Code)
            .Select(x => new PermissionResponse(x.Id, x.Code, x.Name, x.Module)).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<SystemSettingResponse>> ListSystemSettingsAsync(CancellationToken ct)
    {
        return await db.SystemSettings.AsNoTracking().OrderBy(x => x.SettingKey)
            .Select(x => new SystemSettingResponse(x.Id, x.SettingKey, x.SettingValue, x.Description, x.IsSensitive, x.CreatedAtUtc, x.ModifiedAtUtc))
            .ToArrayAsync(ct);
    }

    public async Task<SystemSettingResponse> UpdateSystemSettingAsync(long id, UpdateSystemSettingRequest request, long currentUserId, CancellationToken ct)
    {
        var setting = await db.SystemSettings.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AdministrationValidationException("System setting was not found.");
        if (request.SettingValue is null) throw new AdministrationValidationException("Setting value is required.");
        if (request.SettingValue.Length > 4000) throw new AdministrationValidationException("Setting value cannot exceed 4,000 characters.");

        setting.SettingValue = request.SettingValue;
        setting.ModifiedAtUtc = DateTime.UtcNow;
        setting.ModifiedByUserId = currentUserId;
        await db.SaveChangesAsync(ct);
        return new SystemSettingResponse(setting.Id, setting.SettingKey, setting.SettingValue, setting.Description, setting.IsSensitive, setting.CreatedAtUtc, setting.ModifiedAtUtc);
    }

    private async Task<Role> GetRoleAsync(string roleName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(roleName)) throw new AdministrationValidationException("Role is required.");
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Name == roleName.Trim(), ct);
        return role ?? throw new AdministrationValidationException($"Role '{roleName.Trim()}' was not found.");
    }

    private async Task EnsureRoleCanBeAssignedAsync(string roleName, long currentUserId, CancellationToken ct)
    {
        if (roleName is RoleNames.SuperAdministrator or RoleNames.GroupAdministrator)
        {
            var isSuper = await db.UserRoles.AnyAsync(x => x.UserId == currentUserId && x.Role.Name == RoleNames.SuperAdministrator, ct);
            if (!isSuper)
                throw new AdministrationValidationException("Only a Super Administrator can assign an administrator role.");
        }
    }

    private async Task<bool> IsLastActiveSuperAdministratorAsync(long excludedUserId, CancellationToken ct)
        => await db.Users.AnyAsync(x => x.IsActive && x.Id != excludedUserId && x.UserRoles.Any(ur => ur.Role.Name == RoleNames.SuperAdministrator), ct) == false;

    private async Task<UserResponse> GetUserResponseAsync(long userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleAsync(x => x.Id == userId, ct);
        return MapUser(user);
    }

    private static UserResponse MapUser(ApplicationUser user) => new(
        user.Id, user.UserName, user.FullName, user.Email, user.IsActive, user.MustChangePassword,
        user.FailedLoginCount, user.LockedUntilUtc, user.LastLoginAtUtc, user.PasswordChangedAtUtc,
        user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToArray(),
        user.CreatedAtUtc, user.CreatedByUserId, user.ModifiedAtUtc, user.ModifiedByUserId);

    private static RoleResponse MapRole(Role role) => new(
        role.Id, role.Name, role.Description,
        role.RolePermissions.Select(x => x.Permission.Code).OrderBy(x => x).ToArray(),
        role.CreatedAtUtc, role.ModifiedAtUtc);

    private static void ValidateUserName(string value)
    {
        var v = value?.Trim() ?? string.Empty;
        if (v.Length < 3 || v.Length > 100) throw new AdministrationValidationException("User name must be between 3 and 100 characters.");
        if (v.Any(char.IsWhiteSpace)) throw new AdministrationValidationException("User name cannot contain spaces.");
    }

    private static void ValidateFullName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 200) throw new AdministrationValidationException("Full name is required and cannot exceed 200 characters.");
    }

    private static void ValidatePassword(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 8) throw new AdministrationValidationException("Password must contain at least 8 characters.");
        if (!value.Any(char.IsUpper) || !value.Any(char.IsLower) || !value.Any(char.IsDigit))
            throw new AdministrationValidationException("Password must contain at least one uppercase letter, one lowercase letter and one digit.");
    }
}
