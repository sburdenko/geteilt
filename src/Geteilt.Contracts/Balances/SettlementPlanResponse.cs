namespace Geteilt.Contracts.Balances;

/// <param name="Transfers">Empty when everyone is square — a valid answer, not a missing one.</param>
public sealed record SettlementPlanResponse(
    string BaseCurrency,
    IReadOnlyList<PlannedTransferDto> Transfers,
    DateTimeOffset CalculatedAt);
