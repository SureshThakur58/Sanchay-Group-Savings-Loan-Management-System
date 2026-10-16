using System.Net.Http.Headers;
using System.Net.Http.Json;
using GroupFinance.Blazor.Authentication;

namespace GroupFinance.Blazor.Services;

public sealed record LoginRequest(string UserName, string Password);
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, long UserId, string UserName, string FullName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions, bool MustChangePassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed class AuthApiClient(HttpClient http, CustomAuthenticationStateProvider authState)
{
    public async Task<(bool Success, string? Error, LoginResponse? Response)> LoginAsync(string userName, string password)
    {
        using var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(userName, password));
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return (false, error?.Message ?? "Login failed.", null);
        }
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (result is null) return (false, "The API returned an invalid login response.", null);
        authState.SetSession(result.AccessToken, result.ExpiresAtUtc, result.UserId, result.UserName, result.FullName, result.Roles, result.Permissions);
        return (true, null, result);
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest(currentPassword, newPassword))
        };
        AddToken(request);
        var response = await http.SendAsync(request);

        if (response.IsSuccessStatusCode)
            return (true, null);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        return (false, error?.Message ?? "Password change failed.");
    }

    public void Logout() => authState.ClearSession();

    private void AddToken(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(authState.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
    }

    private sealed record ErrorResponse(string Message);
}
