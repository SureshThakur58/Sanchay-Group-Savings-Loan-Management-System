using GroupFinance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/health/database")]
public sealed class DatabaseHealthController(GroupFinanceDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Ok(new { status = "Healthy", database = "GroupFinance" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy", database = "GroupFinance" });
    }
}
