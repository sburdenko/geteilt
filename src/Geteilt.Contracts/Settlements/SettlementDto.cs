namespace Geteilt.Contracts.Settlements;

public sealed record SettlementDto(
    Guid Id,
    Guid FromMemberId,
    string FromName,
    Guid ToMemberId,
    string ToName,
    decimal Amount,
    string Currency,
    DateOnly Date,
    SettlementMethod Method,
    string? Note,
    DateTimeOffset CreatedAt);
