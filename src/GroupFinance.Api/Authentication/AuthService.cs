using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GroupFinance.Api.Models;
using GroupFinance.Core.Entities;
using GroupFinance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GroupFinance.Api.Authentication;

public sealed class AuthService(GroupFinanceDbContext db, IOptions<JwtOptions> jwtOptions)
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private readonly PasswordHasher<ApplicationUser> passwordHasher = new();

    public async Task<LoginResponse?> LoginAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.UserName == userName, cancellationToken);
        if (user is null || !user.IsActive) return null;
        if (user.LockedUntilUtc is not null && user.LockedUntilUtc > DateTime.UtcNow) return null;
        if (user.LockedUntilUtc is not null && user.LockedUntilUtc <= DateTime.UtcNow) user.LockedUntilUtc = null;

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.LastLoginAtUtc = DateTime.UtcNow;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwordHasher.HashPassword(user, password);
        await db.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles.Select(x => x.Role.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissions = await db.UserRoles.Where(x => x.UserId == user.Id)
            .SelectMany(x => x.Role.RolePermissions.Select(rp => rp.Permission.Code))
            .Distinct().OrderBy(x => x).ToArrayAsync(cancellationToken);
        var token = CreateToken(user, roles, permissions, out var expiresAtUtc);
        return new LoginResponse(token, expiresAtUtc, user.Id, user.UserName, user.FullName, roles, permissions, user.MustChangePassword);
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || !user.IsActive) return null;
        var roles = user.UserRoles.Select(x => x.Role.Name).Distinct().ToArray();
        var permissions = await db.UserRoles.Where(x => x.UserId == user.Id)
            .SelectMany(x => x.Role.RolePermissions.Select(rp => rp.Permission.Code)).Distinct().OrderBy(x => x).ToArrayAsync(cancellationToken);
        return new CurrentUserResponse(user.Id, user.UserName, user.FullName, roles, permissions, user.MustChangePassword);
    }

    public async Task<bool> ChangePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || !user.IsActive) return false;
        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed) return false;
        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.PasswordChangedAtUtc = DateTime.UtcNow;
        user.MustChangePassword = false;
        user.ModifiedAtUtc = DateTime.UtcNow;
        user.ModifiedByUserId = userId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private string CreateToken(ApplicationUser user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions, out DateTime expiresAtUtc)
    {
        var options = jwtOptions.Value;
        if (Encoding.UTF8.GetByteCount(options.SecretKey) < 32)
            throw new InvalidOperationException("Jwt:SecretKey must contain at least 32 bytes.");
        expiresAtUtc = DateTime.UtcNow.AddMinutes(options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("full_name", user.FullName)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims, DateTime.UtcNow, expiresAtUtc, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
