using Geteilt.Application.Ports;
using Geteilt.Contracts.Groups;
using Geteilt.Domain.Groups;
using Geteilt.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Geteilt.Application.Groups;

public sealed class GroupService(IAppDbContext dbContext, TimeProvider timeProvider) : IGroupService
{
    public async Task<IReadOnlyList<GroupDto>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.VisibleTo(userId)
            .OrderByDescending(group => group.CreatedAt)
            .ToDto(dbContext, userId)
            .ToListAsync(cancellationToken);

    public Task<GroupDto?> GetAsync(Guid userId, Guid groupId, CancellationToken cancellationToken) =>
        dbContext.VisibleTo(userId)
            .Where(group => group.Id == groupId)
            .ToDto(dbContext, userId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<GroupDto> CreateAsync(
        Guid userId,
        CreateGroupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset now = timeProvider.GetUtcNow();

        Group group = new(Guid.CreateVersion7(), request.Name, request.BaseCurrency, userId, now);

        // The creator joins their own group as owner in the same save. A group with no members
        // would be unreachable the moment it was created — nobody could see it.
        string displayName = await dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => user.DisplayName)
            .SingleAsync(cancellationToken);

        Member owner = new(Guid.CreateVersion7(), group.Id, displayName, now, userId, MemberRole.Owner);

        dbContext.Groups.Add(group);
        dbContext.Members.Add(owner);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new GroupDto(group.Id, group.Name, group.BaseCurrency, group.CreatedAt, group.ArchivedAt, 1, 0m);
    }

    public async Task<GroupDto?> UpdateAsync(
        Guid userId,
        Guid groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Group? group = await dbContext.VisibleTo(userId)
            .SingleOrDefaultAsync(candidate => candidate.Id == groupId, cancellationToken);

        if (group is null)
        {
            return null;
        }

        await RequireOwnerAsync(userId, groupId, "change a group", cancellationToken);

        if (request.Name is not null)
        {
            // Throws if the group is archived — renaming is a write, and archived groups take none.
            group.Rename(request.Name);
        }

        if (request.IsArchived is { } archived)
        {
            if (archived)
            {
                group.Archive(timeProvider.GetUtcNow());
            }
            else
            {
                group.Unarchive();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(userId, groupId, cancellationToken);
    }

    private async Task RequireOwnerAsync(
        Guid userId,
        Guid groupId,
        string action,
        CancellationToken cancellationToken)
    {
        // 403 rather than 404 here on purpose: the caller can see this group, so its existence is
        // not a secret from them — they simply lack the role.
        bool isOwner = await dbContext.Members.AnyAsync(
            member => member.GroupId == groupId
                && member.UserId == userId
                && member.RemovedAt == null
                && member.Role == MemberRole.Owner,
            cancellationToken);

        if (!isOwner)
        {
            throw GroupException.OwnerOnly(action);
        }
    }
}
