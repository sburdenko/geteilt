using Geteilt.Contracts.Balances;

namespace Geteilt.Application.Balances;

public interface IBalanceService
{
    /// <summary>Null when the group is not there for this caller.</summary>
    Task<BalancesResponse?> GetAsync(Guid userId, Guid groupId, CancellationToken cancellationToken);

    /// <summary>Who should pay whom to settle up. Computed on demand, stored nowhere.</summary>
    Task<SettlementPlanResponse?> GetPlanAsync(Guid userId, Guid groupId, CancellationToken cancellationToken);
}
