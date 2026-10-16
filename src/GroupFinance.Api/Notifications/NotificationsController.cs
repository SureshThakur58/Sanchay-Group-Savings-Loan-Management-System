using System.Security.Claims;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Notifications;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy=PermissionCodes.NotificationsView)]
    public async Task<ActionResult<NotificationListResponse>> List([FromQuery] bool unreadOnly=false,[FromQuery] string? type=null,CancellationToken ct=default){try{return Ok(await service.ListAsync(CurrentUserId(),unreadOnly,type,ct));}catch(NotificationValidationException ex){return BadRequest(new{message=ex.Message});}}
    [HttpGet("unread-count")]
    [Authorize(Policy=PermissionCodes.NotificationsView)]
    public async Task<ActionResult<int>> UnreadCount(CancellationToken ct){try{return Ok(await service.GetUnreadCountAsync(CurrentUserId(),ct));}catch(NotificationValidationException ex){return BadRequest(new{message=ex.Message});}}
    [HttpPost("evaluate")]
    [Authorize(Policy=PermissionCodes.NotificationsManage)]
    public async Task<ActionResult<NotificationEvaluationResponse>> Evaluate([FromQuery] long groupId,[FromQuery] DateTime? asOfDate=null,[FromQuery] int daysAhead=7,CancellationToken ct=default){try{return Ok(await service.EvaluateAsync(groupId,asOfDate,daysAhead,ct));}catch(NotificationValidationException ex){return BadRequest(new{message=ex.Message});}}
    [HttpPost("{id:long}/read")]
    [Authorize(Policy=PermissionCodes.NotificationsView)]
    public async Task<IActionResult> MarkRead(long id,CancellationToken ct){try{await service.MarkReadAsync(id,CurrentUserId(),ct);return NoContent();}catch(NotificationValidationException ex){return BadRequest(new{message=ex.Message});}}
    [HttpPost("read-all")]
    [Authorize(Policy=PermissionCodes.NotificationsView)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct){try{await service.MarkAllReadAsync(CurrentUserId(),ct);return NoContent();}catch(NotificationValidationException ex){return BadRequest(new{message=ex.Message});}}
    private long CurrentUserId()=>long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)??throw new UnauthorizedAccessException("Authenticated user id is required."));
}
