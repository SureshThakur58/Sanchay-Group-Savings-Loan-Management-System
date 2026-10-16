using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Backup;

[ApiController]
[Route("api/backup")]
[Authorize(Roles = "SuperAdministrator")]
public sealed class BackupController(BackupService service) : ControllerBase
{
    [HttpGet("configuration")]
    [Authorize(Policy = PermissionCodes.BackupsView)]
    public ActionResult<BackupConfigurationResponse> Configuration()
        => Ok(service.GetConfiguration());

    [HttpGet("files")]
    [Authorize(Policy = PermissionCodes.BackupsView)]
    public async Task<ActionResult<IReadOnlyList<BackupFileResponse>>> Files(CancellationToken ct)
        => Ok(await service.ListBackupsAsync(ct));

    [HttpPost("create")]
    [Authorize(Policy = PermissionCodes.BackupsManage)]
    public async Task<ActionResult<BackupResultResponse>> Create(CancellationToken ct)
    {
        try { return Ok(await service.CreateBackupAsync(ct)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("verify")]
    [Authorize(Policy = PermissionCodes.BackupsManage)]
    public async Task<ActionResult> Verify([FromBody] string fileName, CancellationToken ct)
    {
        try
        {
            var verified = await service.VerifyBackupAsync(fileName, ct);
            return Ok(new { verified, message = verified ? "Backup verification succeeded." : "Backup verification failed." });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("retention")]
    [Authorize(Policy = PermissionCodes.BackupsManage)]
    public async Task<ActionResult<RetentionResponse>> Retention(CancellationToken ct)
    {
        try { return Ok(await service.ApplyRetentionAsync(ct)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("schedules")]
    [Authorize(Policy = PermissionCodes.BackupsView)]
    public async Task<ActionResult<IReadOnlyList<BackupScheduleResponse>>> Schedules(CancellationToken ct)
        => Ok(await service.ListSchedulesAsync(ct));

    [HttpPost("schedules")]
    [Authorize(Policy = PermissionCodes.BackupsManage)]
    public async Task<ActionResult<BackupScheduleResponse>> SaveSchedule(SaveBackupScheduleRequest request, CancellationToken ct)
    {
        try { return Ok(await service.SaveScheduleAsync(null, request, ct)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("schedules/{id:guid}")]
    [Authorize(Policy = PermissionCodes.BackupsManage)]
    public async Task<ActionResult<BackupScheduleResponse>> UpdateSchedule(Guid id, SaveBackupScheduleRequest request, CancellationToken ct)
    {
        try { return Ok(await service.SaveScheduleAsync(id, request, ct)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("schedules/{id:guid}")]
    [Authorize(Policy = PermissionCodes.BackupsManage)]
    public async Task<IActionResult> DeleteSchedule(Guid id, CancellationToken ct)
    {
        try { await service.DeleteScheduleAsync(id, ct); return NoContent(); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("restore")]
    [Authorize(Policy = PermissionCodes.BackupsManage)]
    public async Task<ActionResult<RestoreResponse>> Restore([FromBody] string fileName, CancellationToken ct)
    {
        try { return Ok(await service.RestoreBackupAsync(fileName, ct)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }
}
