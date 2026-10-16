using System.Security.Claims;
using GroupFinance.Api.OverdueLoans;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/overdue-loans")]
[Authorize]
public sealed class OverdueLoansController(OverdueLoanService service) : ControllerBase
{
    [HttpPost("evaluate")]
    [Authorize(Policy = PermissionCodes.LoansManage)]
    public async Task<ActionResult<OverdueEvaluationResponse>> Evaluate(EvaluateOverdueLoansRequest request, CancellationToken ct)
    {
        try { return Ok(await service.EvaluateAsync(request.GroupId, request.AsOfDate, ct)); }
        catch (OverdueLoanValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<IReadOnlyCollection<OverdueLoanSummaryResponse>>> List([FromQuery] long groupId, [FromQuery] DateTime? asOfDate, CancellationToken ct)
    {
        try { return Ok(await service.ListAsync(groupId, asOfDate, ct)); }
        catch (OverdueLoanValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("loans/{loanId:long}")]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<OverdueLoanSummaryResponse>> GetLoan(long loanId, [FromQuery] DateTime? asOfDate, CancellationToken ct)
    {
        var result = await service.GetLoanAsync(loanId, asOfDate, ct);
        return result is null ? NotFound(new { message = "Loan not found or has no overdue installments." }) : Ok(result);
    }

    [HttpGet("loans/{loanId:long}/installments")]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<IReadOnlyCollection<OverdueLoanScheduleResponse>>> GetInstallments(long loanId, [FromQuery] DateTime? asOfDate, CancellationToken ct)
    {
        var result = await service.GetInstallmentsAsync(loanId, asOfDate, ct);
        return result is null ? NotFound(new { message = "Loan not found." }) : Ok(result);
    }
}
