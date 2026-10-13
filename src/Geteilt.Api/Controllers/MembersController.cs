using Geteilt.Api.Authentication;
using Geteilt.Application.Members;
using Geteilt.Contracts.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Geteilt.Api.Controllers;

[ApiController]
[Route("api/groups/{groupId:guid}/members")]
[Authorize]
public sealed class MembersController(IMemberService members) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MemberDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> List(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MemberDto>? result = await members.ListAsync(User.CurrentUserId(), groupId, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<MemberDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MemberDto>> Add(
        Guid groupId,
        AddMemberRequest request,
        CancellationToken cancellationToken)
    {
        MemberDto? member = await members.AddAsync(User.CurrentUserId(), groupId, request, cancellationToken);

        return member is null
            ? NotFound()
            : CreatedAtAction(nameof(List), new { groupId }, member);
    }

    [HttpPatch("{memberId:guid}")]
    [ProducesResponseType<MemberDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MemberDto>> Patch(
        Guid groupId,
        Guid memberId,
        UpdateMemberRequest request,
        CancellationToken cancellationToken)
    {
        MemberDto? member = await members.UpdateAsync(
            User.CurrentUserId(), groupId, memberId, request, cancellationToken);

        return member is null ? NotFound() : Ok(member);
    }

    /// <summary>Only possible while nothing depends on them; afterwards they can only be marked as left.</summary>
    [HttpDelete("{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(Guid groupId, Guid memberId, CancellationToken cancellationToken)
    {
        bool removed = await members.RemoveAsync(User.CurrentUserId(), groupId, memberId, cancellationToken);

        return removed ? NoContent() : NotFound();
    }
}
