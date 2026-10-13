namespace Geteilt.Contracts.Balances;

/// <param name="Balances">
/// Everyone in the group, including those sitting at exactly zero and those who have left. Leaving
/// anyone out would break the one property worth checking: the balances add up to zero.
/// </param>
public sealed record BalancesResponse(
    string BaseCurrency,
    IReadOnlyList<BalanceDto> Balances,
    DateTimeOffset CalculatedAt);
