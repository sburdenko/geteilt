using Geteilt.Contracts.Balances;

namespace Geteilt.Web.Api;

public sealed class BalancesApi(HttpClient http)
{
    public Task<Result<BalancesResponse>> GetAsync(
        Guid groupId,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<BalancesResponse>(
            http, ApiCall.Get($"api/groups/{groupId}/balances"), cancellationToken);

    public Task<Result<SettlementPlanResponse>> PlanAsync(
        Guid groupId,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<SettlementPlanResponse>(
            http, ApiCall.Get($"api/groups/{groupId}/settlement-plan"), cancellationToken);
}
