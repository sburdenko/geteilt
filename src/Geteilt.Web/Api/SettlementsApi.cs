using Geteilt.Contracts.Settlements;

namespace Geteilt.Web.Api;

public sealed class SettlementsApi(HttpClient http)
{
    public Task<Result<SettlementDto>> CreateAsync(
        Guid groupId,
        CreateSettlementRequest request,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<SettlementDto>(
            http, ApiCall.Post($"api/groups/{groupId}/settlements", request, idempotencyKey), cancellationToken);
}
