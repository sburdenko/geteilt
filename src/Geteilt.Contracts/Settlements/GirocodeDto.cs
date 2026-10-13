namespace Geteilt.Contracts.Settlements;

/// <summary>
/// A Girocode together with the fields it was drawn from. A screen that shows the code and, under
/// it, the IBAN to check by eye needs both from one read: two reads can disagree, and then the
/// person checks the wrong account.
/// </summary>
/// <param name="Remittance">The payment reference exactly as it is inside the code.</param>
/// <param name="PngBase64">The code as a PNG, base64-encoded.</param>
public sealed record GirocodeDto(
    string BeneficiaryName,
    string Iban,
    decimal Amount,
    string Currency,
    string Remittance,
    string PngBase64);
