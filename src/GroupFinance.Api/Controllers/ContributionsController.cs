using System.Security.Claims;
using GroupFinance.Api.Contributions;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/contributions")]
[Authorize]
public sealed class ContributionsController(ContributionService service) : ControllerBase
{
    [HttpGet("plans")]
    [Authorize(Policy = PermissionCodes.ContributionsView)]
    public async Task<ActionResult<IReadOnlyCollection<ContributionPlanResponse>>> ListPlans([FromQuery] long groupId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        try { return Ok(await service.ListPlansAsync(groupId, includeInactive, ct)); }
        catch (ContributionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("plans/{id:long}")]
    [Authorize(Policy = PermissionCodes.ContributionsView)]
    public async Task<ActionResult<ContributionPlanResponse>> GetPlan(long id, CancellationToken ct)
    {
        var result = await service.GetPlanAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("plans")]
    [Authorize(Policy = PermissionCodes.ContributionsManage)]
    public async Task<ActionResult<ContributionPlanResponse>> CreatePlan(CreateContributionPlanRequest request, CancellationToken ct)
    {
        try { return StatusCode(StatusCodes.Status201Created, await service.CreatePlanAsync(request, GetUserId(), ct)); }
        catch (ContributionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("plans/{id:long}")]
    [Authorize(Policy = PermissionCodes.ContributionsManage)]
    public async Task<ActionResult<ContributionPlanResponse>> UpdatePlan(long id, UpdateContributionPlanRequest request, CancellationToken ct)
    {
        try
        {
            var result = await service.UpdatePlanAsync(id, request, GetUserId(), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ContributionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("schedules/generate")]
    [Authorize(Policy = PermissionCodes.ContributionsManage)]
    public async Task<ActionResult<ContributionScheduleGenerationResponse>> GenerateSchedules(GenerateContributionSchedulesRequest request, CancellationToken ct)
    {
        try { return Ok(await service.GenerateSchedulesAsync(request, GetUserId(), ct)); }
        catch (ContributionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("schedules")]
    [Authorize(Policy = PermissionCodes.ContributionsView)]
    public async Task<ActionResult<IReadOnlyCollection<ContributionScheduleResponse>>> ListSchedules(
        [FromQuery] long groupId, [FromQuery] long? memberId, [FromQuery] long? planId,
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] string? status, CancellationToken ct)
    {
        try { return Ok(await service.ListSchedulesAsync(groupId, memberId, planId, fromDate, toDate, status, ct)); }
        catch (ContributionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("schedules/{id:long}")]
    [Authorize(Policy = PermissionCodes.ContributionsView)]
    public async Task<ActionResult<ContributionScheduleResponse>> GetSchedule(long id, CancellationToken ct)
    {
        var result = await service.GetScheduleAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("schedules/{id:long}/payments")]
    [Authorize(Policy = PermissionCodes.ContributionsManage)]
    public async Task<ActionResult<ContributionPaymentResponse>> RecordPayment(long id, CreateContributionPaymentRequest request, CancellationToken ct)
    {
        try { return StatusCode(StatusCodes.Status201Created, await service.RecordPaymentAsync(id, request, GetUserId(), ct)); }
        catch (ContributionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("schedules/{id:long}/waive-late-fee")]
    [Authorize(Policy = PermissionCodes.ContributionsManage)]
    public async Task<ActionResult<ContributionScheduleResponse>> WaiveLateFee(long id, WaiveContributionLateFeeRequest request, CancellationToken ct)
    {
        try
        {
            var result = await service.WaiveLateFeeAsync(id, request, GetUserId(), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ContributionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user id is missing."));
}
