using System.Security.Claims;
using GroupFinance.Api.Audit;
using GroupFinance.Core.Security;
using GroupFinance.Api.FinancialTransactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public sealed class AuditController(AuditService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.AuditLogsView)]
    public async Task<ActionResult<IReadOnlyList<AuditLogResponse>>> List(
        [FromQuery] long groupId,
        [FromQuery] string? entityName,
        [FromQuery] long? entityId,
        [FromQuery] string? action,
        [FromQuery] long? userId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        try
        {
            return Ok(await service.ListAsync(groupId, entityName, entityId, action, userId, fromDate, toDate, ct));
        }
        catch (AuditValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

}

[ApiController]
[Route("api/reversals")]
[Authorize]
public sealed class ReversalsController(ReversalService service) : ControllerBase
{
    [HttpPost("{transactionId:long}")]
    [Authorize(Policy = PermissionCodes.AuditLogsManage)]
    public async Task<ActionResult<FinancialTransactionResponse>> Reverse(
        long transactionId,
        ReverseFinancialTransactionRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await service.ReverseAsync(
                transactionId,
                request,
                GetUserId(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                ct);

            return Ok(result);
        }
        catch (AuditValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (FinancialTransactionValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private long GetUserId() =>
        long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user id is missing."));
}
