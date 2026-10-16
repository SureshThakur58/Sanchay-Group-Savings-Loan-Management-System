using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Dashboard;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(
        [FromQuery] long groupId,
        [FromQuery] DateTime? asOfDate,
        CancellationToken ct)
    {
        try
        {
            return Ok(await service.GetAsync(groupId, asOfDate, ct));
        }
        catch (DashboardValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
