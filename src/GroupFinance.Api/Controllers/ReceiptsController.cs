using GroupFinance.Api.Receipts;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/receipts")]
[Authorize]
public sealed class ReceiptsController(ReceiptService service) : ControllerBase
{
    [HttpGet("{transactionId:long}")]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsView)]
    public async Task<ActionResult<ReceiptResponse>> Get(long transactionId, CancellationToken ct)
    {
        try
        {
            var result = await service.GetAsync(transactionId, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ReceiptValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
