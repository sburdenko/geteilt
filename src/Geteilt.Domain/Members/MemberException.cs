namespace Geteilt.Domain.Members;

/// <summary>Something cannot be done to a member.</summary>
public sealed class MemberException : DomainException
{
    /// <summary>A group holds at most twenty; typical groups have three to six.</summary>
    public const int MaxMembersPerGroup = 20;

    public static readonly DomainError GroupMemberLimit =
        new("group_member_limit", DomainErrorKind.Validation, "The group is full");

    public static readonly DomainError HasActivity =
        new("member_has_activity", DomainErrorKind.Conflict, "The member has expenses or a balance");

    public static readonly DomainError AlreadyInGroup =
        new("member_already_in_group", DomainErrorKind.Conflict, "That person is already in this group");

    private MemberException(DomainError error, string message)
        : base(error, message)
    {
    }

    public static MemberException GroupIsFull() => new(
        GroupMemberLimit,
        $"A group holds at most {MaxMembersPerGroup} members.");

    /// <summary>
    /// A conflict rather than a validation failure: removing them is impossible only because of
    /// what they have already done. Marking them as left is the way out — they keep counting in
    /// the history, and "all balances add up to zero" keeps holding.
    /// </summary>
    public static MemberException HasActivityAndCannotBeDeleted(Guid memberId) => new(
        HasActivity,
        $"Member {memberId} appears in expenses or settlements and cannot be deleted, only marked as left.");

    public static MemberException AlreadyJoined() => new(
        AlreadyInGroup,
        "That account is already a member of this group.");
}
