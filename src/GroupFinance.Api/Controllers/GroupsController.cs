using System.Security.Claims;
using GroupFinance.Api.Groups;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize]
public sealed class GroupsController(GroupService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.GroupsView)]
    public async Task<ActionResult<IReadOnlyCollection<GroupResponse>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListAsync(cancellationToken));

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionCodes.GroupsView)]
    public async Task<ActionResult<GroupResponse>> Get(long id, CancellationToken cancellationToken)
    {
        var group = await service.GetAsync(id, cancellationToken);
        return group is null ? NotFound(new { message = "Group not found." }) : Ok(group);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.SuperAdministrator, Policy = PermissionCodes.GroupsManage)]
    public async Task<ActionResult<GroupResponse>> Create(CreateGroupRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var group = await service.CreateAsync(request, GetUserId(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = group.Id }, group);
        }
        catch (GroupValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionCodes.GroupsManage)]
    public async Task<ActionResult<GroupResponse>> Update(long id, UpdateGroupRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var group = await service.UpdateAsync(id, request, GetUserId(), cancellationToken);
            return group is null ? NotFound(new { message = "Group not found." }) : Ok(group);
        }
        catch (GroupValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:long}/settings")]
    [Authorize(Policy = PermissionCodes.GroupsView)]
    public async Task<ActionResult<GroupSettingsResponse>> GetSettings(long id, CancellationToken cancellationToken)
    {
        var settings = await service.GetSettingsAsync(id, cancellationToken);
        return settings is null ? NotFound(new { message = "Group settings not found." }) : Ok(settings);
    }

    [HttpPut("{id:long}/settings")]
    [Authorize(Policy = PermissionCodes.GroupsManage)]
    public async Task<ActionResult<GroupSettingsResponse>> UpdateSettings(long id, GroupSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await service.UpdateSettingsAsync(id, request, GetUserId(), cancellationToken);
            return settings is null ? NotFound(new { message = "Group or group settings not found." }) : Ok(settings);
        }
        catch (GroupValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user id is missing."));
}
