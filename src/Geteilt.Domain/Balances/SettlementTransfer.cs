namespace Geteilt.Domain.Balances;

/// <summary>
/// One line of a settlement plan: a suggestion that this member pays that one. It becomes a
/// <see cref="Entities.Settlement"/> only when somebody says the transfer happened.
/// </summary>
public sealed record SettlementTransfer(Guid FromMemberId, Guid ToMemberId, decimal Amount);
