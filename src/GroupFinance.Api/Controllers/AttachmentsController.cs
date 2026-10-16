using GroupFinance.Api.FileStorage;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/attachments")]
[Authorize]
public sealed class AttachmentsController(AttachmentService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.AttachmentsView)]
    public async Task<ActionResult<IReadOnlyList<AttachmentResponse>>> List(
        [FromQuery] string entityName,
        [FromQuery] long entityId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await service.ListAsync(entityName, entityId, ct));
        }
        catch (AttachmentValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("upload")]
    [Authorize(Policy = PermissionCodes.AttachmentsManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentResponse>> Upload(
        [FromForm] string entityName,
        [FromForm] long entityId,
        IFormFile? file,
        CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            var result = await service.UploadAsync(entityName, entityId, file, userId, ct);
            return CreatedAtAction(nameof(List), new { entityName, entityId }, result);
        }
        catch (AttachmentValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:long}/download")]
    [Authorize(Policy = PermissionCodes.AttachmentsView)]
    public async Task<IActionResult> Download(long id, CancellationToken ct)
    {
        try
        {
            var result = await service.OpenDownloadAsync(id, ct);
            if (result is null) return NotFound();

            Response.ContentLength = result.Length;
            return File(result.Content, result.ContentType, result.FileName, enableRangeProcessing: true);
        }
        catch (AttachmentValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionCodes.AttachmentsManage)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        try
        {
            await service.DeleteAsync(id, GetUserId(), ct);
            return NoContent();
        }
        catch (AttachmentValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private long? GetUserId()
    {
        var value = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(value, out var id) ? id : null;
    }
}
