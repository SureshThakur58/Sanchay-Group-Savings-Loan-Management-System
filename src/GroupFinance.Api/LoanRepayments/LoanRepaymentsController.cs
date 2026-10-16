using GroupFinance.Core.Security;
using GroupFinance.Api.FinancialTransactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.LoanRepayments;

[ApiController]
[Route("api/loan-repayments")]
public sealed class LoanRepaymentsController(LoanRepaymentService service) : ControllerBase
{
    [HttpPost("{loanId:long}")]
    [Authorize(Policy = PermissionCodes.LoansManage)]
    public async Task<ActionResult<LoanRepaymentResponse>> Repay(long loanId, LoanRepaymentRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            return Ok(await service.RepayAsync(loanId, request, userId, ct));
        }
        catch (LoanRepaymentValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (FinancialTransactionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{loanId:long}/payments")]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<IReadOnlyCollection<LoanPaymentSummaryResponse>>> List(long loanId, CancellationToken ct)
    {
        try { return Ok(await service.ListAsync(loanId, ct)); }
        catch (LoanRepaymentValidationException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("payments/{paymentId:long}")]
    [Authorize(Policy = PermissionCodes.LoansView)]
    public async Task<ActionResult<LoanPaymentSummaryResponse>> Get(long paymentId, CancellationToken ct)
    {
        var result = await service.GetPaymentAsync(paymentId, ct);
        return result is null ? NotFound(new { message = "Loan payment not found." }) : Ok(result);
    }

    private long GetUserId()
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(value, out var id) && id > 0 ? id : throw new UnauthorizedAccessException("Authenticated user id is required.");
    }
}
