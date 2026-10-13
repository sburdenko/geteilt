using Geteilt.Domain.Girocode;

namespace Geteilt.Application.Settlements;

public interface IGirocodeService
{
    /// <summary>Null when the settlement is not visible to this caller.</summary>
    Task<GirocodeRequest?> ForSettlementAsync(Guid userId, Guid settlementId, CancellationToken cancellationToken);

    /// <summary>Null when the group is not visible to this caller.</summary>
    Task<GirocodeRequest?> ForPreviewAsync(
        Guid userId,
        Guid groupId,
        Guid toMemberId,
        decimal amount,
        CancellationToken cancellationToken);
}
