using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Administration;

[ApiController]
[Route("api/administration")]
[Authorize]
public sealed class AdministrationController(AdministrationService service) : ControllerBase
{
    [HttpGet("users")]
    [Authorize(Policy = PermissionCodes.UsersView)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> Users(CancellationToken ct)
        => Ok(await service.ListUsersAsync(ct));

    [HttpPost("users")]
    [Authorize(Policy = PermissionCodes.UsersManage)]
    public async Task<ActionResult<UserResponse>> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        try { return Ok(await service.CreateUserAsync(request, GetUserId(), ct)); }
        catch (AdministrationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("users/{id:long}")]
    [Authorize(Policy = PermissionCodes.UsersManage)]
    public async Task<ActionResult<UserResponse>> UpdateUser(long id, UpdateUserRequest request, CancellationToken ct)
    {
        try { return Ok(await service.UpdateUserAsync(id, request, GetUserId(), ct)); }
        catch (AdministrationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("users/{id:long}/reset-password")]
    [Authorize(Policy = PermissionCodes.UsersManage)]
    public async Task<IActionResult> ResetPassword(long id, ResetUserPasswordRequest request, CancellationToken ct)
    {
        try { await service.ResetPasswordAsync(id, request, GetUserId(), ct); return NoContent(); }
        catch (AdministrationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("roles")]
    [Authorize(Policy = PermissionCodes.RolesView)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> Roles(CancellationToken ct) => Ok(await service.ListRolesAsync(ct));

    [HttpPut("roles/{id:long}")]
    [Authorize(Policy = PermissionCodes.RolesManage)]
    public async Task<ActionResult<RoleResponse>> UpdateRole(long id, UpdateRoleRequest request, CancellationToken ct)
    {
        try { return Ok(await service.UpdateRoleAsync(id, request, GetUserId(), ct)); }
        catch (AdministrationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("roles/{id:long}/permissions")]
    [Authorize(Policy = PermissionCodes.RolesManage)]
    public async Task<ActionResult<RoleResponse>> UpdateRolePermissions(long id, UpdateRolePermissionsRequest request, CancellationToken ct)
    {
        try { return Ok(await service.UpdateRolePermissionsAsync(id, request, GetUserId(), ct)); }
        catch (AdministrationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("permissions")]
    [Authorize(Policy = PermissionCodes.RolesView)]
    public async Task<ActionResult<IReadOnlyList<PermissionResponse>>> Permissions(CancellationToken ct) => Ok(await service.ListPermissionsAsync(ct));

    [HttpGet("system-settings")]
    [Authorize(Policy = PermissionCodes.SystemSettingsManage)]
    public async Task<ActionResult<IReadOnlyList<SystemSettingResponse>>> SystemSettings(CancellationToken ct) => Ok(await service.ListSystemSettingsAsync(ct));

    [HttpPut("system-settings/{id:long}")]
    [Authorize(Policy = PermissionCodes.SystemSettingsManage)]
    public async Task<ActionResult<SystemSettingResponse>> UpdateSystemSetting(long id, UpdateSystemSettingRequest request, CancellationToken ct)
    {
        try { return Ok(await service.UpdateSystemSettingAsync(id, request, GetUserId(), ct)); }
        catch (AdministrationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private long GetUserId()
    {
        var value = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("Authenticated user ID is missing.");
    }
}
