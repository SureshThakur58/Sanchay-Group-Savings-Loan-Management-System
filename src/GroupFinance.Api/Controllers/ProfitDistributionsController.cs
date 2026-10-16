using GroupFinance.Api.ProfitDistributions;
using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/profit-distributions")]
[Authorize]
public sealed class ProfitDistributionsController(ProfitDistributionService service) : ControllerBase
{
    [HttpPost("preview")]
    [Authorize(Policy = PermissionCodes.InterestProfitView)]
    public async Task<ActionResult<ProfitDistributionResponse>> Preview(ProfitDistributionPreviewRequest request, CancellationToken ct)
    {
        try { return Ok(await service.PreviewAsync(request, ct)); }
        catch (ProfitDistributionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost]
    [Authorize(Policy = PermissionCodes.InterestProfitManage)]
    public async Task<ActionResult<ProfitDistributionResponse>> Create(ProfitDistributionRequest request, CancellationToken ct)
    {
        try
        {
            var result = await service.CreateAsync(request, CurrentUserId(), ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }
        catch (ProfitDistributionValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (FinancialTransactionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.InterestProfitView)]
    public async Task<ActionResult<IReadOnlyCollection<ProfitDistributionResponse>>> List([FromQuery] long groupId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, CancellationToken ct)
    {
        try { return Ok(await service.ListAsync(groupId, fromDate, toDate, ct)); }
        catch (ProfitDistributionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionCodes.InterestProfitView)]
    public async Task<ActionResult<ProfitDistributionResponse>> Get(long id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    private long CurrentUserId() => long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
}
