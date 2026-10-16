using System.Security.Claims;
using GroupFinance.Api.Authentication;
using GroupFinance.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthController(AuthService authService, ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required." });

        var response = await authService.LoginAsync(request.UserName.Trim(), request.Password, cancellationToken);
        if (response is null)
        {
            logger.LogWarning("Authentication failed for username {UserName} from {IpAddress}.", request.UserName, HttpContext.Connection.RemoteIpAddress?.ToString());
            return Unauthorized(new { message = "Invalid username or password, or the account is locked/inactive." });
        }
        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var response = await authService.GetCurrentUserAsync(userId, cancellationToken);
        return response is null ? Unauthorized() : Ok(response);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { message = "Current password and new password are required." });
        if (request.NewPassword.Length < 8)
            return BadRequest(new { message = "New password must be at least 8 characters." });
        if (request.NewPassword == request.CurrentPassword)
            return BadRequest(new { message = "New password must be different from the current password." });
        var ok = await authService.ChangePasswordAsync(GetUserId(), request.CurrentPassword, request.NewPassword, cancellationToken);
        return ok ? NoContent() : BadRequest(new { message = "Current password is incorrect." });
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user id is missing."));
}
