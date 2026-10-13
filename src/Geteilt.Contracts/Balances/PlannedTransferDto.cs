namespace Geteilt.Contracts.Balances;

/// <param name="CanGenerateQr">
/// False when the receiver has no IBAN anywhere — the transfer is still valid, it just cannot be
/// turned into a Girocode, and the client should offer to fill one in rather than hide the row.
/// </param>
public sealed record PlannedTransferDto(
    Guid FromMemberId,
    string FromName,
    Guid ToMemberId,
    string ToName,
    decimal Amount,
    string Currency,
    string? ToIban,
    bool CanGenerateQr);
