using GroupFinance.Api.Ledger;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/ledger")]
[Authorize]
public sealed class LedgerController(LedgerService service) : ControllerBase
{
    [HttpGet("group")]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsView)]
    public async Task<ActionResult<IReadOnlyCollection<LedgerEntryResponse>>> GroupLedger(
        [FromQuery] long groupId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] long? financialAccountId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await service.GetGroupLedgerAsync(groupId, fromDate, toDate, financialAccountId, ct));
        }
        catch (LedgerValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("member")]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsView)]
    public async Task<ActionResult<IReadOnlyCollection<LedgerEntryResponse>>> MemberLedger(
        [FromQuery] long groupId,
        [FromQuery] long memberId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        try
        {
            return Ok(await service.GetMemberLedgerAsync(groupId, memberId, fromDate, toDate, ct));
        }
        catch (LedgerValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("trial-balance")]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsView)]
    public async Task<ActionResult<TrialBalanceResponse>> TrialBalance(
        [FromQuery] long groupId,
        [FromQuery] DateTime asOfDate,
        CancellationToken ct)
    {
        try
        {
            return Ok(await service.GetTrialBalanceAsync(groupId, asOfDate, ct));
        }
        catch (LedgerValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
