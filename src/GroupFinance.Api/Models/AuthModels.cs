namespace GroupFinance.Api.Models;

public sealed record LoginRequest(string UserName, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, long UserId, string UserName, string FullName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions, bool MustChangePassword);
public sealed record CurrentUserResponse(long UserId, string UserName, string FullName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions, bool MustChangePassword);
