using Geteilt.Contracts.Settlements;

namespace Geteilt.Application.Settlements;

public interface ISettlementService
{
    Task<SettlementDto?> CreateAsync(
        Guid userId,
        Guid groupId,
        CreateSettlementRequest request,
        CancellationToken cancellationToken);

    Task<SettlementDto?> GetAsync(Guid userId, Guid groupId, Guid settlementId, CancellationToken cancellationToken);

    Task<SettlementPage?> ListAsync(
        Guid userId,
        Guid groupId,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid groupId, Guid settlementId, CancellationToken cancellationToken);
}
