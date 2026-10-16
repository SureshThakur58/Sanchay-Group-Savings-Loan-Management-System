using System.Security.Claims;
using GroupFinance.Api.LoanApplications;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/loan-applications")]
[Authorize]
public sealed class LoanApplicationsController(LoanApplicationService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<IReadOnlyCollection<LoanApplicationSummaryResponse>>> List([FromQuery] long groupId, [FromQuery] string? status, [FromQuery] long? memberId, CancellationToken ct)
    {
        try { return Ok(await service.ListAsync(groupId, status, memberId, ct)); }
        catch (LoanApplicationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<LoanApplicationResponse>> Get(long id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        return result is null ? NotFound(new { message = "Loan application not found." }) : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = PermissionCodes.LoansManage)]
    public async Task<ActionResult<LoanApplicationResponse>> Create(CreateLoanApplicationRequest request, CancellationToken ct)
    {
        try { var result = await service.CreateAsync(request, GetUserId(), ct); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
        catch (LoanApplicationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionCodes.LoansManage)]
    public async Task<ActionResult<LoanApplicationResponse>> Update(long id, UpdateLoanApplicationRequest request, CancellationToken ct)
    {
        try { var result = await service.UpdateAsync(id, request, GetUserId(), ct); return result is null ? NotFound() : Ok(result); }
        catch (LoanApplicationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:long}/submit")]
    [Authorize(Policy = PermissionCodes.LoansManage)]
    public async Task<ActionResult<LoanApplicationResponse>> Submit(long id, CancellationToken ct)
    {
        try { var result = await service.SubmitAsync(id, GetUserId(), ct); return result is null ? NotFound() : Ok(result); }
        catch (LoanApplicationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:long}/approve")]
    [Authorize(Roles = RoleNames.SuperAdministrator + "," + RoleNames.GroupAdministrator)]
    [Authorize(Policy = PermissionCodes.LoansManage)]
    public async Task<ActionResult<LoanApplicationResponse>> Approve(long id, DecisionRequest request, CancellationToken ct)
    {
        try { var result = await service.ApproveAsync(id, request.Remarks, GetUserId(), ct); return result is null ? NotFound() : Ok(result); }
        catch (LoanApplicationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:long}/reject")]
    [Authorize(Roles = RoleNames.SuperAdministrator + "," + RoleNames.GroupAdministrator)]
    [Authorize(Policy = PermissionCodes.LoansManage)]
    public async Task<ActionResult<LoanApplicationResponse>> Reject(long id, DecisionRequest request, CancellationToken ct)
    {
        try { var result = await service.RejectAsync(id, request.Remarks, GetUserId(), ct); return result is null ? NotFound() : Ok(result); }
        catch (LoanApplicationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:long}/cancel")]
    [Authorize(Policy = PermissionCodes.LoansManage)]
    public async Task<ActionResult<LoanApplicationResponse>> Cancel(long id, CancellationToken ct)
    {
        try { var result = await service.CancelAsync(id, GetUserId(), ct); return result is null ? NotFound() : Ok(result); }
        catch (LoanApplicationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:long}/guarantors")]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<IReadOnlyCollection<LoanApplicationGuarantorResponse>>> ListGuarantors(long id, CancellationToken ct)
    {
        var result = await service.ListGuarantorsAsync(id, ct);
        return result is null ? NotFound(new { message = "Loan application not found." }) : Ok(result);
    }

    [HttpPost("{id:long}/guarantors")]
    [Authorize(Policy = PermissionCodes.LoansManage)]
    public async Task<ActionResult<LoanApplicationGuarantorResponse>> AddGuarantor(long id, AddLoanApplicationGuarantorRequest request, CancellationToken ct)
    {
        try { return StatusCode(StatusCodes.Status201Created, await service.AddGuarantorAsync(id, request, GetUserId(), ct)); }
        catch (LoanApplicationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:long}/guarantors/{guarantorId:long}")]
    [Authorize(Policy = PermissionCodes.LoansManage)]
    public async Task<IActionResult> RemoveGuarantor(long id, long guarantorId, CancellationToken ct)
    {
        try { return await service.RemoveGuarantorAsync(id, guarantorId, GetUserId(), ct) ? NoContent() : NotFound(); }
        catch (LoanApplicationValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user id is missing."));
}

public sealed record DecisionRequest(string? Remarks);
