using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace GroupFinance.Blazor.Authentication;

public sealed class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private ClaimsPrincipal currentUser = Anonymous;
    private string? token;
    private DateTime expiresAtUtc;

    public string? AccessToken => token;

    public void SetSession(string accessToken, DateTime expiry, long userId, string userName, string fullName, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        token = accessToken;
        expiresAtUtc = expiry;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userName),
            new("full_name", fullName)
        };
        claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));
        claims.AddRange(permissions.Select(x => new Claim("permission", x)));
        currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(currentUser)));
    }

    public void ClearSession()
    {
        token = null;
        expiresAtUtc = default;
        currentUser = Anonymous;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (token is not null && expiresAtUtc > DateTime.UtcNow)
            return Task.FromResult(new AuthenticationState(currentUser));
        if (token is not null) ClearSession();
        return Task.FromResult(new AuthenticationState(Anonymous));
    }
}
