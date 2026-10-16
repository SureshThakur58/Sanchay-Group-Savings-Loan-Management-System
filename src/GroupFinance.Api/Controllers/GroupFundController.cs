using System.Security.Claims;
using GroupFinance.Api.GroupFund;
using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/group-fund")]
[Authorize]
public sealed class GroupFundController(GroupFundService service) : ControllerBase
{
    [HttpGet("accounts")]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsView)]
    public async Task<ActionResult<IReadOnlyCollection<GroupFundAccountResponse>>> Accounts([FromQuery] long groupId, CancellationToken ct)
    {
        try { return Ok(await service.GetAccountsAsync(groupId, ct)); }
        catch (GroupFundValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("accounts/{id:long}")]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsManage)]
    public async Task<ActionResult<GroupFundAccountResponse>> UpdateAccount(long id, UpdateGroupFundAccountRequest request, CancellationToken ct)
    {
        try { return Ok(await service.UpdateAccountAsync(id, request, GetUserId(), ct)); }
        catch (GroupFundValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("opening-fund")]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsManage)]
    public async Task<ActionResult<OpeningFundResponse>> OpeningFund(CreateOpeningFundRequest request, CancellationToken ct)
    {
        try { return Ok(await service.CreateOpeningFundAsync(request, GetUserId(), ct)); }
        catch (GroupFundValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (FinancialTransactionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("transfers")]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsManage)]
    public async Task<ActionResult<FundTransferResponse>> Transfer(FundTransferRequest request, CancellationToken ct)
    {
        try { return Ok(await service.TransferAsync(request, GetUserId(), ct)); }
        catch (GroupFundValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (FinancialTransactionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("summary")]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsView)]
    public async Task<ActionResult<GroupFundSummaryResponse>> Summary([FromQuery] long groupId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, CancellationToken ct)
    {
        try { return Ok(await service.GetSummaryAsync(groupId, fromDate, toDate, ct)); }
        catch (GroupFundValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user id is missing."));
}
