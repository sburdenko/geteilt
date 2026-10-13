namespace Geteilt.Contracts.Settlements;

/// <summary>
/// A Girocode for a transfer that has not been recorded yet — the QR screen shows it before
/// anybody presses "paid".
/// </summary>
public sealed record GirocodePreviewRequest(Guid ToMemberId, decimal Amount);
