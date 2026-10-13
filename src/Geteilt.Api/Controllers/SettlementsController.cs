using Geteilt.Api.Authentication;
using Geteilt.Application.Settlements;
using Geteilt.Contracts.Settlements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Geteilt.Api.Controllers;

[ApiController]
[Route("api/groups/{groupId:guid}/settlements")]
[Authorize]
public sealed class SettlementsController(ISettlementService settlements) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SettlementPage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettlementPage>> List(
        Guid groupId,
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int? limit = null)
    {
        SettlementPage? page = await settlements.ListAsync(
            User.CurrentUserId(), groupId, cursor, limit, cancellationToken);

        return page is null ? NotFound() : Ok(page);
    }

    [HttpGet("{settlementId:guid}")]
    [ProducesResponseType<SettlementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettlementDto>> Get(
        Guid groupId,
        Guid settlementId,
        CancellationToken cancellationToken)
    {
        SettlementDto? settlement = await settlements.GetAsync(
            User.CurrentUserId(), groupId, settlementId, cancellationToken);

        return settlement is null ? NotFound() : Ok(settlement);
    }

    [HttpPost]
    [ProducesResponseType<SettlementDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SettlementDto>> Create(
        Guid groupId,
        CreateSettlementRequest request,
        CancellationToken cancellationToken)
    {
        SettlementDto? settlement = await settlements.CreateAsync(
            User.CurrentUserId(), groupId, request, cancellationToken);

        return settlement is null
            ? NotFound()
            : CreatedAtAction(nameof(Get), new { groupId, settlementId = settlement.Id }, settlement);
    }

    /// <summary>Soft delete; the balances go back to what they were before the repayment.</summary>
    [HttpDelete("{settlementId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid groupId, Guid settlementId, CancellationToken cancellationToken)
    {
        bool deleted = await settlements.DeleteAsync(
            User.CurrentUserId(), groupId, settlementId, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
