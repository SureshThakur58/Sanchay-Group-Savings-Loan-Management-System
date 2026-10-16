using GroupFinance.Api.Models;
using GroupFinance.Core.Constants;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> Get()
    {
        return Ok(new HealthResponse(
            "Healthy",
            ApplicationConstants.ApplicationName,
            environment.EnvironmentName,
            DateTime.UtcNow));
    }
}
