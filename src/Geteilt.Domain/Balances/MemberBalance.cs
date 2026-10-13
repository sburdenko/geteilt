namespace Geteilt.Domain.Balances;

/// <summary>
/// What a group owes a member, or a member owes the group. Positive means the group owes them,
/// negative means they owe the group.
/// </summary>
public sealed record MemberBalance(Guid MemberId, decimal Balance)
{
    public bool IsSettled => Balance == 0m;

    public bool IsOwed => Balance > 0m;

    public bool Owes => Balance < 0m;
}
