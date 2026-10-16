using System.Security.Claims;
using GroupFinance.Api.Members;
using GroupFinance.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupFinance.Api.Controllers;

[ApiController]
[Route("api/members")]
[Authorize]
public sealed class MembersController(MemberService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.MembersView)]
    public async Task<ActionResult<IReadOnlyCollection<MemberResponse>>> List([FromQuery] long groupId, [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        if (groupId <= 0) return BadRequest(new { message = "groupId is required." });
        try { return Ok(await service.ListAsync(groupId, includeInactive, cancellationToken)); }
        catch (MemberValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("next-number")]
    [Authorize(Policy = PermissionCodes.MembersView)]
    public async Task<ActionResult<object>> GetNextNumber([FromQuery] long groupId, CancellationToken cancellationToken)
    {
        if (groupId <= 0) return BadRequest(new { message = "groupId is required." });
        try { return Ok(new { memberNumber = await service.GetNextMemberNumberAsync(groupId, cancellationToken) }); }
        catch (MemberValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionCodes.MembersView)]
    public async Task<ActionResult<MemberResponse>> Get(long id, CancellationToken cancellationToken)
    {
        var member = await service.GetAsync(id, cancellationToken);
        return member is null ? NotFound(new { message = "Member not found." }) : Ok(member);
    }

    [HttpPost]
    [Authorize(Policy = PermissionCodes.MembersManage)]
    public async Task<ActionResult<MemberResponse>> Create(CreateMemberRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var member = await service.CreateAsync(request, GetUserId(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = member.Id }, member);
        }
        catch (MemberValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionCodes.MembersManage)]
    public async Task<ActionResult<MemberResponse>> Update(long id, UpdateMemberRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var member = await service.UpdateAsync(id, request, GetUserId(), cancellationToken);
            return member is null ? NotFound(new { message = "Member not found." }) : Ok(member);
        }
        catch (MemberValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:long}/nominees")]
    [Authorize(Policy = PermissionCodes.MembersView)]
    public async Task<ActionResult<IReadOnlyCollection<MemberNomineeResponse>>> ListNominees(long id, CancellationToken cancellationToken)
    {
        var nominees = await service.ListNomineesAsync(id, cancellationToken);
        return nominees is null ? NotFound(new { message = "Member not found." }) : Ok(nominees);
    }

    [HttpPost("{id:long}/nominees")]
    [Authorize(Policy = PermissionCodes.MembersManage)]
    public async Task<ActionResult<MemberNomineeResponse>> AddNominee(long id, CreateMemberNomineeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var nominee = await service.AddNomineeAsync(id, request, GetUserId(), cancellationToken);
            return CreatedAtAction(nameof(ListNominees), new { id }, nominee);
        }
        catch (MemberValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:long}/nominees/{nomineeId:long}")]
    [Authorize(Policy = PermissionCodes.MembersManage)]
    public async Task<ActionResult<MemberNomineeResponse>> UpdateNominee(long id, long nomineeId, UpdateMemberNomineeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var nominee = await service.UpdateNomineeAsync(id, nomineeId, request, GetUserId(), cancellationToken);
            return nominee is null ? NotFound(new { message = "Member or nominee not found." }) : Ok(nominee);
        }
        catch (MemberValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:long}/nominees/{nomineeId:long}")]
    [Authorize(Policy = PermissionCodes.MembersManage)]
    public async Task<IActionResult> RemoveNominee(long id, long nomineeId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.RemoveNomineeAsync(id, nomineeId, GetUserId(), cancellationToken);
            if (result is null) return NotFound(new { message = "Member not found." });
            if (!result.Value) return NotFound(new { message = "Nominee not found." });
            return NoContent();
        }
        catch (MemberValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user id is missing."));
}
