namespace Geteilt.Domain.Groups;

/// <summary>Something cannot be done to a group in the state it is in.</summary>
public sealed class GroupException : DomainException
{
    public static readonly DomainError GroupArchived =
        new("group_archived", DomainErrorKind.Conflict, "The group is archived");

    public static readonly DomainError InsufficientRole =
        new("insufficient_role", DomainErrorKind.Forbidden, "This action is for the group owner");

    private GroupException(DomainError error, string message)
        : base(error, message)
    {
    }

    /// <summary>A conflict, not a validation failure: the same request would have worked yesterday.</summary>
    public static GroupException Archived(Guid groupId) => new(
        GroupArchived,
        $"Group {groupId} is archived and accepts no changes. Unarchive it first.");

    public static GroupException OwnerOnly(string action) => new(
        InsufficientRole,
        $"Only the owner of a group can {action}.");
}
