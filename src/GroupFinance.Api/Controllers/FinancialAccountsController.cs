using System.Security.Claims;
using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/financial-accounts")]
[Authorize]
public sealed class FinancialAccountsController(FinancialTransactionService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsView)]
    public async Task<ActionResult<IReadOnlyCollection<FinancialAccountResponse>>> List([FromQuery] long groupId, CancellationToken cancellationToken)
    {
        if (groupId <= 0) return BadRequest(new { message = "groupId is required." });
        return Ok(await service.GetAccountsAsync(groupId, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsManage)]
    public async Task<ActionResult<FinancialAccountResponse>> Create(CreateFinancialAccountRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user id is missing."));
            return Ok(await service.CreateAccountAsync(request, userId, cancellationToken));
        }
        catch (FinancialTransactionValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:long}/balance")]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsView)]
    public async Task<ActionResult<FinancialAccountBalanceResponse>> Balance(long id, CancellationToken cancellationToken)
    {
        var response = await service.GetAccountBalanceAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
