using GroupFinance.Api.Assets;
using GroupFinance.Api.FinancialTransactions;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize]
public sealed class AssetsController(AssetService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.AssetsView)]
    public async Task<ActionResult<IReadOnlyCollection<AssetResponse>>> List(
        [FromQuery] long groupId,
        [FromQuery] bool includeInactive,
        [FromQuery] string? category,
        CancellationToken ct)
    {
        try { return Ok(await service.ListAssetsAsync(groupId, includeInactive, category, ct)); }
        catch (AssetValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionCodes.AssetsView)]
    public async Task<ActionResult<AssetResponse>> Get(long id, CancellationToken ct)
    {
        var result = await service.GetAssetAsync(id, ct);
        return result is null ? NotFound(new { message = "Asset not found." }) : Ok(result);
    }

    [HttpGet("{id:long}/transactions")]
    [Authorize(Policy = PermissionCodes.AssetsView)]
    public async Task<ActionResult<IReadOnlyCollection<AssetTransactionResponse>>> Transactions(long id, CancellationToken ct)
    {
        try { return Ok(await service.ListTransactionsAsync(id, ct)); }
        catch (AssetValidationException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    [Authorize(Policy = PermissionCodes.AssetsManage)]
    public async Task<ActionResult<AssetResponse>> Create(CreateAssetRequest request, CancellationToken ct)
    {
        try { return Ok(await service.CreateAssetAsync(request, CurrentUserId(), ct)); }
        catch (AssetValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (FinancialTransactionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:long}/sell")]
    [Authorize(Policy = PermissionCodes.AssetsManage)]
    public async Task<ActionResult<AssetTransactionResponse>> Sell(long id, SellAssetRequest request, CancellationToken ct)
    {
        try { return Ok(await service.SellAssetAsync(id, request, CurrentUserId(), ct)); }
        catch (AssetValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (FinancialTransactionValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private long CurrentUserId() => long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
}
