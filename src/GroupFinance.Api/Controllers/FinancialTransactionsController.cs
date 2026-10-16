using System.Security.Claims;
using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/financial-transactions")]
[Authorize]
public sealed class FinancialTransactionsController(FinancialTransactionService service) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsManage)]
    public async Task<ActionResult<FinancialTransactionResponse>> Create(CreateFinancialTransactionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.CreateAsync(request, GetUserId(), cancellationToken));
        }
        catch (FinancialTransactionValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsView)]
    public async Task<ActionResult<IReadOnlyCollection<FinancialTransactionResponse>>> List(
        [FromQuery] long groupId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? transactionType,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (groupId <= 0) return BadRequest(new { message = "groupId is required." });
        return Ok(await service.ListAsync(groupId, fromDate, toDate, transactionType, status, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionCodes.FinancialTransactionsView)]
    public async Task<ActionResult<FinancialTransactionResponse>> Get(long id, CancellationToken cancellationToken)
    {
        var response = await service.GetAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user id is missing."));
}
