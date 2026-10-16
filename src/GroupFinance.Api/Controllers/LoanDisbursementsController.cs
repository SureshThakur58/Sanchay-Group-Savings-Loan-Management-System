using System.Security.Claims;
using GroupFinance.Api.LoanDisbursements;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/loan-disbursements")]
[Authorize]
public sealed class LoanDisbursementsController(LoanDisbursementService service) : ControllerBase
{
    [HttpPost("{applicationId:long}")]
    [Authorize(Policy = PermissionCodes.LoansManage)]
    public async Task<ActionResult<LoanDisbursementResponse>> Disburse(long applicationId, LoanDisbursementRequest request, CancellationToken ct)
    {
        try { return Ok(await service.DisburseAsync(applicationId, request, GetUserId(), ct)); }
        catch (LoanDisbursementValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("loans")]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<IReadOnlyCollection<LoanSummaryResponse>>> ListLoans([FromQuery] long groupId, [FromQuery] string? status, [FromQuery] long? memberId, CancellationToken ct)
    {
        try { return Ok(await service.ListLoansAsync(groupId, status, memberId, ct)); }
        catch (LoanDisbursementValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("loans/{id:long}")]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<LoanSummaryResponse>> GetLoan(long id, CancellationToken ct)
    {
        var result = await service.GetLoanAsync(id, ct);
        return result is null ? NotFound(new { message = "Loan not found." }) : Ok(result);
    }

    [HttpGet("loans/{id:long}/schedule")]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<IReadOnlyCollection<LoanScheduleResponse>>> GetSchedule(long id, CancellationToken ct)
    {
        var result = await service.GetScheduleAsync(id, ct);
        return result is null ? NotFound(new { message = "Loan not found." }) : Ok(result);
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user id is missing."));
}
