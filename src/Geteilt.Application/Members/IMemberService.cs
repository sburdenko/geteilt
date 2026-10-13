using Geteilt.Contracts.Members;

namespace Geteilt.Application.Members;

/// <summary>
/// Members of a group. Every method returns null — meaning 404 — when the caller cannot see the
/// group at all, so a group they are not in stays invisible rather than forbidden.
/// </summary>
public interface IMemberService
{
    Task<IReadOnlyList<MemberDto>?> ListAsync(Guid userId, Guid groupId, CancellationToken cancellationToken);

    Task<MemberDto?> AddAsync(
        Guid userId,
        Guid groupId,
        AddMemberRequest request,
        CancellationToken cancellationToken);

    Task<MemberDto?> UpdateAsync(
        Guid userId,
        Guid groupId,
        Guid memberId,
        UpdateMemberRequest request,
        CancellationToken cancellationToken);

    /// <summary>False when the group or the member is not there for this caller.</summary>
    Task<bool> RemoveAsync(Guid userId, Guid groupId, Guid memberId, CancellationToken cancellationToken);
}
