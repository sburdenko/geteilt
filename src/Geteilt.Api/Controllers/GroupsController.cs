using Geteilt.Api.Authentication;
using Geteilt.Application.Groups;
using Geteilt.Contracts.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Geteilt.Api.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize]
public sealed class GroupsController(IGroupService groups) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GroupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GroupDto>>> List(CancellationToken cancellationToken) =>
        Ok(await groups.ListAsync(User.CurrentUserId(), cancellationToken));

    /// <summary>A group the caller is not a member of answers 404, never 403.</summary>
    [HttpGet("{groupId:guid}")]
    [ProducesResponseType<GroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupDto>> Get(Guid groupId, CancellationToken cancellationToken)
    {
        GroupDto? group = await groups.GetAsync(User.CurrentUserId(), groupId, cancellationToken);

        return group is null ? NotFound() : Ok(group);
    }

    [HttpPost]
    [ProducesResponseType<GroupDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GroupDto>> Create(
        CreateGroupRequest request,
        CancellationToken cancellationToken)
    {
        GroupDto group = await groups.CreateAsync(User.CurrentUserId(), request, cancellationToken);

        return CreatedAtAction(nameof(Get), new { groupId = group.Id }, group);
    }

    [HttpPatch("{groupId:guid}")]
    [ProducesResponseType<GroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GroupDto>> Patch(
        Guid groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken)
    {
        GroupDto? group = await groups.UpdateAsync(User.CurrentUserId(), groupId, request, cancellationToken);

        return group is null ? NotFound() : Ok(group);
    }
}
