using Geteilt.Application.Groups;
using Geteilt.Application.Ports;
using Geteilt.Contracts.Members;
using Geteilt.Domain.Groups;
using Geteilt.Domain.Members;
using Microsoft.EntityFrameworkCore;
using ContractMemberRole = Geteilt.Contracts.Members.MemberRole;
using DomainMemberRole = Geteilt.Domain.Members.MemberRole;

namespace Geteilt.Application.Members;

public sealed class MemberService(IAppDbContext dbContext, TimeProvider timeProvider) : IMemberService
{
    public async Task<IReadOnlyList<MemberDto>?> ListAsync(
        Guid userId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        if (!await CanSeeAsync(userId, groupId, cancellationToken))
        {
            return null;
        }

        return await dbContext.Members
            .AsNoTracking()
            .Where(member => member.GroupId == groupId)
            .OrderBy(member => member.JoinedAt)
            // Projected inline rather than through the ToDto helper: a method call on the entity
            // cannot be translated, and EF would silently fall back to loading every row first.
            .Select(member => new MemberDto(
                member.Id,
                member.UserId,
                member.DisplayName,
                member.Iban,
                member.Role == DomainMemberRole.Owner ? ContractMemberRole.Owner : ContractMemberRole.Member,
                member.UserId != null,
                member.RemovedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<MemberDto?> AddAsync(
        Guid userId,
        Guid groupId,
        AddMemberRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Group? group = await RequireOwnerOfVisibleGroupAsync(userId, groupId, "add members", cancellationToken);

        if (group is null)
        {
            return null;
        }

        group.RejectIfArchived();

        int members = await dbContext.Members.CountAsync(member => member.GroupId == groupId, cancellationToken);

        if (members >= MemberException.MaxMembersPerGroup)
        {
            throw MemberException.GroupIsFull();
        }

        // No address means somebody taking part without an account — the friend who never installs
        // anything. They get a member row with no user behind it and count in every balance.
        Guid? linkedUserId = null;

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            string email = request.Email.Trim().ToLowerInvariant();

            linkedUserId = await dbContext.Users
                .Where(user => user.Email == email)
                .Select(user => (Guid?)user.Id)
                .SingleOrDefaultAsync(cancellationToken);

            // An unknown address is not an error: they simply join without an account, and can be
            // linked later. A known one must not join twice.
            if (linkedUserId is { } known
                && await dbContext.Members.AnyAsync(
                    member => member.GroupId == groupId && member.UserId == known,
                    cancellationToken))
            {
                throw MemberException.AlreadyJoined();
            }
        }

        Member member = new(
            Guid.CreateVersion7(),
            groupId,
            request.DisplayName,
            timeProvider.GetUtcNow(),
            linkedUserId,
            DomainMemberRole.Member,
            request.Iban);

        dbContext.Members.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(member);
    }

    public async Task<MemberDto?> UpdateAsync(
        Guid userId,
        Guid groupId,
        Guid memberId,
        UpdateMemberRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await CanSeeAsync(userId, groupId, cancellationToken))
        {
            return null;
        }

        Member? member = await dbContext.Members
            .SingleOrDefaultAsync(candidate => candidate.Id == memberId && candidate.GroupId == groupId, cancellationToken);

        if (member is null)
        {
            return null;
        }

        // Changing a role is an owner's business; editing a display name or an IBAN is not.
        if (request.Role is not null)
        {
            await RequireOwnerAsync(userId, groupId, "change roles", cancellationToken);
        }

        if (request.DisplayName is not null)
        {
            member.Rename(request.DisplayName);
        }

        if (request.Iban is not null)
        {
            member.SetIban(request.Iban);
        }

        if (request.Role is { } role)
        {
            member.ChangeRole(ToDomain(role));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(member);
    }

    public async Task<bool> RemoveAsync(
        Guid userId,
        Guid groupId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        Group? group = await RequireOwnerOfVisibleGroupAsync(userId, groupId, "remove members", cancellationToken);

        if (group is null)
        {
            return false;
        }

        Member? member = await dbContext.Members
            .SingleOrDefaultAsync(candidate => candidate.Id == memberId && candidate.GroupId == groupId, cancellationToken);

        if (member is null)
        {
            return false;
        }

        if (await HasActivityAsync(memberId, cancellationToken))
        {
            throw MemberException.HasActivityAndCannotBeDeleted(memberId);
        }

        // Nothing depends on them yet, so the row can go. Once anything does, the only option is
        // marking them as left — see MemberException.HasActivityAndCannotBeDeleted.
        dbContext.Members.Remove(member);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>Anything at all that would lose meaning if the row disappeared.</summary>
    private async Task<bool> HasActivityAsync(Guid memberId, CancellationToken cancellationToken)
    {
        bool inExpenses = await dbContext.Expenses.AnyAsync(
            expense => expense.PaidByMemberId == memberId
                || expense.Shares.Any(share => share.MemberId == memberId),
            cancellationToken);

        if (inExpenses)
        {
            return true;
        }

        // Query filters hide soft-deleted rows, and that is right here too: a deleted expense
        // no longer counts towards any balance, so it no longer holds the member in place.
        return await dbContext.Settlements.AnyAsync(
            settlement => settlement.FromMemberId == memberId || settlement.ToMemberId == memberId,
            cancellationToken);
    }

    private Task<bool> CanSeeAsync(Guid userId, Guid groupId, CancellationToken cancellationToken) =>
        dbContext.VisibleTo(userId).AnyAsync(group => group.Id == groupId, cancellationToken);

    private async Task<Group?> RequireOwnerOfVisibleGroupAsync(
        Guid userId,
        Guid groupId,
        string action,
        CancellationToken cancellationToken)
    {
        Group? group = await dbContext.VisibleTo(userId)
            .SingleOrDefaultAsync(candidate => candidate.Id == groupId, cancellationToken);

        if (group is null)
        {
            return null;
        }

        await RequireOwnerAsync(userId, groupId, action, cancellationToken);

        return group;
    }

    private async Task RequireOwnerAsync(
        Guid userId,
        Guid groupId,
        string action,
        CancellationToken cancellationToken)
    {
        bool isOwner = await dbContext.Members.AnyAsync(
            member => member.GroupId == groupId
                && member.UserId == userId
                && member.RemovedAt == null
                && member.Role == DomainMemberRole.Owner,
            cancellationToken);

        if (!isOwner)
        {
            throw GroupException.OwnerOnly(action);
        }
    }

    private static MemberDto ToDto(Member member) => new(
        member.Id,
        member.UserId,
        member.DisplayName,
        member.Iban,
        member.Role == DomainMemberRole.Owner ? ContractMemberRole.Owner : ContractMemberRole.Member,
        member.UserId is not null,
        member.RemovedAt);

    private static DomainMemberRole ToDomain(ContractMemberRole role) =>
        role == ContractMemberRole.Owner ? DomainMemberRole.Owner : DomainMemberRole.Member;
}
