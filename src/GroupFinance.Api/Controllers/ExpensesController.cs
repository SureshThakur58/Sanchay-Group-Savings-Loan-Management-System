using System.Security.Claims;
using GroupFinance.Api.Expenses;
using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize]
public sealed class ExpensesController(ExpenseService service) : ControllerBase
{
    [HttpGet("categories")]
    [Authorize(Policy = PermissionCodes.ExpensesView)]
    public async Task<ActionResult<IReadOnlyCollection<ExpenseCategoryResponse>>> ListCategories([FromQuery] long groupId, [FromQuery] bool includeInactive, CancellationToken ct)
    {
        try { return Ok(await service.ListCategoriesAsync(groupId, includeInactive, ct)); }
        catch (ExpenseValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("categories/{id:long}")]
    [Authorize(Policy = PermissionCodes.ExpensesView)]
    public async Task<ActionResult<ExpenseCategoryResponse>> GetCategory(long id, CancellationToken ct)
    {
        var result = await service.GetCategoryAsync(id, ct);
        return result is null ? NotFound(new { message = "Expense category not found." }) : Ok(result);
    }

    [HttpPost("categories")]
    [Authorize(Policy = PermissionCodes.ExpensesManage)]
    public async Task<ActionResult<ExpenseCategoryResponse>> CreateCategory(CreateExpenseCategoryRequest request, CancellationToken ct)
    {
        try { return Ok(await service.CreateCategoryAsync(request, CurrentUserId(), ct)); }
        catch (ExpenseValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("categories/{id:long}")]
    [Authorize(Policy = PermissionCodes.ExpensesManage)]
    public async Task<ActionResult<ExpenseCategoryResponse>> UpdateCategory(long id, UpdateExpenseCategoryRequest request, CancellationToken ct)
    {
        try
        {
            var result = await service.UpdateCategoryAsync(id, request, CurrentUserId(), ct);
            return result is null ? NotFound(new { message = "Expense category not found." }) : Ok(result);
        }
        catch (ExpenseValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet]
    [Authorize(Policy = PermissionCodes.ExpensesView)]
    public async Task<ActionResult<IReadOnlyCollection<ExpenseResponse>>> List([FromQuery] long groupId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] long? categoryId, [FromQuery] string? status, CancellationToken ct)
    {
        try { return Ok(await service.ListExpensesAsync(groupId, fromDate, toDate, categoryId, status, ct)); }
        catch (ExpenseValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionCodes.ExpensesView)]
    public async Task<ActionResult<ExpenseResponse>> Get(long id, CancellationToken ct)
    {
        var result = await service.GetExpenseAsync(id, ct);
        return result is null ? NotFound(new { message = "Expense not found." }) : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = PermissionCodes.ExpensesManage)]
    public async Task<ActionResult<ExpenseResponse>> Create(CreateExpenseRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await service.CreateExpenseAsync(request, CurrentUserId(), ct));
        }
        catch (ExpenseValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (FinancialTransactionValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private long CurrentUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
}
