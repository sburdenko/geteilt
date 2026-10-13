using Geteilt.Contracts.Groups;

namespace Geteilt.Application.Groups;

/// <summary>
/// Groups the caller belongs to. Every method scopes itself to that membership, so a group the
/// caller is not in is invisible rather than forbidden.
/// </summary>
public interface IGroupService
{
    Task<IReadOnlyList<GroupDto>> ListAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns <see langword="null"/> when the group does not exist or is not the caller's.</summary>
    Task<GroupDto?> GetAsync(Guid userId, Guid groupId, CancellationToken cancellationToken);

    Task<GroupDto> CreateAsync(Guid userId, CreateGroupRequest request, CancellationToken cancellationToken);

    Task<GroupDto?> UpdateAsync(
        Guid userId,
        Guid groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken);
}
