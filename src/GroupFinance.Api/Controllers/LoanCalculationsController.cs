using GroupFinance.Api.LoanCalculations;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/loan-calculations")]
[Authorize]
public sealed class LoanCalculationsController(ApiLoanCalculationService service) : ControllerBase
{
    [HttpPost("preview")]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<LoanCalculationResponse>> Preview(LoanCalculationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.CalculateAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
