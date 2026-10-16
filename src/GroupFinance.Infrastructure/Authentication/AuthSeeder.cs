using GroupFinance.Core.Entities;
using GroupFinance.Core.Security;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GroupFinance.Infrastructure.Authentication;

public static class AuthSeeder
{
    public static async Task SeedAsync(GroupFinanceDbContext db, IConfiguration configuration, ILogger logger, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var roles = new Dictionary<string, string>
        {
            [RoleNames.SuperAdministrator] = "Full system access, user management, configuration and administration.",
            [RoleNames.GroupAdministrator] = "Manage the configured group, members and operational records.",
            [RoleNames.Accountant] = "Manage collections, financial transactions and financial reports.",
            [RoleNames.GroupViewer] = "Read-only access to group information and reports."
        };

        foreach (var pair in roles)
        {
            if (!await db.Roles.AnyAsync(x => x.Name == pair.Key, cancellationToken))
            {
                db.Roles.Add(new Role { Name = pair.Key, Description = pair.Value, CreatedAtUtc = now });
            }
        }

        foreach (var code in PermissionCodes.All)
        {
            if (!await db.Permissions.AnyAsync(x => x.Code == code, cancellationToken))
            {
                var parts = code.Split('.', 2);
                db.Permissions.Add(new Permission
                {
                    Code = code,
                    Name = parts.Length == 2 ? $"{parts[0]} {parts[1]}" : code,
                    Module = parts[0],
                    CreatedAtUtc = now
                });
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        var allPermissions = await db.Permissions.ToDictionaryAsync(x => x.Code, cancellationToken);
        var roleEntities = await db.Roles.ToDictionaryAsync(x => x.Name, cancellationToken);

        var super = roleEntities[RoleNames.SuperAdministrator];
        var groupAdmin = roleEntities[RoleNames.GroupAdministrator];
        var accountant = roleEntities[RoleNames.Accountant];
        var viewer = roleEntities[RoleNames.GroupViewer];

        await GrantAllMissingAsync(db, super, allPermissions.Values, now, cancellationToken);
        await GrantMissingAsync(db, groupAdmin, [PermissionCodes.GroupsView, PermissionCodes.GroupsManage, PermissionCodes.MembersView, PermissionCodes.MembersManage, PermissionCodes.ContributionsView, PermissionCodes.ContributionsManage, PermissionCodes.LoansView, PermissionCodes.LoansManage, PermissionCodes.FinancialTransactionsView, PermissionCodes.FinancialTransactionsManage, PermissionCodes.ExpensesView, PermissionCodes.ExpensesManage, PermissionCodes.AssetsView, PermissionCodes.AssetsManage, PermissionCodes.InterestProfitView, PermissionCodes.InterestProfitManage, PermissionCodes.ReportsView, PermissionCodes.AuditLogsView, PermissionCodes.AuditLogsManage, PermissionCodes.NotificationsView, PermissionCodes.NotificationsManage, PermissionCodes.AttachmentsView, PermissionCodes.AttachmentsManage], allPermissions, now, cancellationToken);
        await GrantMissingAsync(db, accountant, [PermissionCodes.MembersView, PermissionCodes.ContributionsView, PermissionCodes.ContributionsManage, PermissionCodes.LoansView, PermissionCodes.LoansManage, PermissionCodes.FinancialTransactionsView, PermissionCodes.FinancialTransactionsManage, PermissionCodes.ExpensesView, PermissionCodes.ExpensesManage, PermissionCodes.InterestProfitView, PermissionCodes.InterestProfitManage, PermissionCodes.ReportsView, PermissionCodes.AuditLogsView, PermissionCodes.NotificationsView, PermissionCodes.AttachmentsView, PermissionCodes.AttachmentsManage], allPermissions, now, cancellationToken);
        await GrantMissingAsync(db, viewer, [PermissionCodes.GroupsView, PermissionCodes.MembersView, PermissionCodes.ContributionsView, PermissionCodes.LoansView, PermissionCodes.FinancialTransactionsView, PermissionCodes.ExpensesView, PermissionCodes.AssetsView, PermissionCodes.InterestProfitView, PermissionCodes.ReportsView, PermissionCodes.NotificationsView, PermissionCodes.AttachmentsView], allPermissions, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (!await db.Users.AnyAsync(cancellationToken))
        {
            var username = configuration["BootstrapAdmin:Username"]?.Trim();
            var password = configuration["BootstrapAdmin:Password"];
            var fullName = configuration["BootstrapAdmin:FullName"]?.Trim() ?? "System Administrator";
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("No users exist and BootstrapAdmin credentials are not configured. No administrator user was created.");
                return;
            }

            var user = new ApplicationUser
            {
                UserName = username,
                FullName = fullName,
                IsActive = true,
                MustChangePassword = true,
                CreatedAtUtc = now,
                PasswordChangedAtUtc = now
            };
            user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = super.Id, CreatedAtUtc = now });
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Bootstrap administrator '{UserName}' was created. Change the password immediately after first login.", username);
        }
    }

    private static async Task GrantAllMissingAsync(GroupFinanceDbContext db, Role role, IEnumerable<Permission> permissions, DateTime now, CancellationToken ct)
    {
        foreach (var permission in permissions)
            await GrantIfMissingAsync(db, role, permission, now, ct);
    }

    private static async Task GrantMissingAsync(GroupFinanceDbContext db, Role role, IEnumerable<string> codes, IReadOnlyDictionary<string, Permission> permissions, DateTime now, CancellationToken ct)
    {
        foreach (var code in codes)
            await GrantIfMissingAsync(db, role, permissions[code], now, ct);
    }

    private static async Task GrantIfMissingAsync(GroupFinanceDbContext db, Role role, Permission permission, DateTime now, CancellationToken ct)
    {
        if (!await db.RolePermissions.AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id, ct))
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, CreatedAtUtc = now });
    }
}
