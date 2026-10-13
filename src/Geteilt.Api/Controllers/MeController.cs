using Geteilt.Api.Authentication;
using Geteilt.Application.Users;
using Geteilt.Contracts.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Geteilt.Api.Controllers;

/// <summary>The signed-in person. Everything here is scoped to whoever holds the token.</summary>
[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(IUserProfileService profiles) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Get(CancellationToken cancellationToken)
    {
        UserDto? user = await profiles.GetAsync(User.CurrentUserId(), cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Fields left out of the body stay as they are; an empty IBAN clears it.</summary>
    [HttpPatch]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> Patch(UpdateMeRequest request, CancellationToken cancellationToken)
    {
        // An invalid IBAN or currency throws a DomainException here and the exception handler turns
        // it into 422 with its error code — no try/catch in controllers.
        UserDto? user = await profiles.UpdateAsync(User.CurrentUserId(), request, cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }
}
