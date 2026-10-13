namespace Geteilt.Contracts.Settlements;

public sealed record SettlementPage(IReadOnlyList<SettlementDto> Items, string? NextCursor);
