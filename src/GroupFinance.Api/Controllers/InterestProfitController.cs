using GroupFinance.Api.InterestProfit;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/interest-profit")]
[Authorize]
public sealed class InterestProfitController(InterestProfitService service) : ControllerBase
{
    [HttpPost("interest/calculate")]
    [Authorize(Policy = PermissionCodes.InterestProfitManage)]
    public async Task<ActionResult<IReadOnlyCollection<InterestCalculationResponse>>> CalculateInterest(CalculateInterestRequest request, CancellationToken ct)
    {
        try { return Ok(await service.CalculateInterestAsync(request, CurrentUserId(), ct)); }
        catch (InterestProfitValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("interest")]
    [Authorize(Policy = PermissionCodes.InterestProfitView)]
    public async Task<ActionResult<IReadOnlyCollection<InterestCalculationResponse>>> ListInterest(
        [FromQuery] long groupId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] long? loanId,
        CancellationToken ct)
    {
        try { return Ok(await service.ListInterestAsync(groupId, fromDate, toDate, loanId, ct)); }
        catch (InterestProfitValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("profit/calculate")]
    [Authorize(Policy = PermissionCodes.InterestProfitView)]
    public async Task<ActionResult<ProfitCalculationResponse>> CalculateProfit(ProfitCalculationRequest request, CancellationToken ct)
    {
        try { return Ok(await service.CalculateProfitAsync(request, ct)); }
        catch (InterestProfitValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private long CurrentUserId() => long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
}
