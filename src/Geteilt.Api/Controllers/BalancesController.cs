using Geteilt.Api.Authentication;
using Geteilt.Application.Balances;
using Geteilt.Contracts.Balances;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Geteilt.Api.Controllers;

[ApiController]
[Route("api/groups/{groupId:guid}")]
[Authorize]
public sealed class BalancesController(IBalanceService balances) : ControllerBase
{
    /// <summary>Everyone in the group, including those at zero — the sum has to come out at zero.</summary>
    [HttpGet("balances")]
    [ProducesResponseType<BalancesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BalancesResponse>> Get(Guid groupId, CancellationToken cancellationToken)
    {
        BalancesResponse? result = await balances.GetAsync(User.CurrentUserId(), groupId, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// An empty list means everyone is square — a valid answer, so 200 rather than 404.
    /// </summary>
    [HttpGet("settlement-plan")]
    [ProducesResponseType<SettlementPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettlementPlanResponse>> GetPlan(
        Guid groupId,
        CancellationToken cancellationToken,
        [FromQuery] string strategy = "greedy")
    {
        // The parameter exists so another planner can appear without changing the URL; today
        // anything but greedy would be a promise the API cannot keep.
        _ = strategy;

        SettlementPlanResponse? plan = await balances.GetPlanAsync(
            User.CurrentUserId(), groupId, cancellationToken);

        return plan is null ? NotFound() : Ok(plan);
    }
}
