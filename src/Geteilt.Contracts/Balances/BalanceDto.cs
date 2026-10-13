namespace Geteilt.Contracts.Balances;

/// <param name="Balance">
/// Positive means the group owes them, negative means they owe the group.
/// </param>
public sealed record BalanceDto(Guid MemberId, string MemberName, decimal Balance);
