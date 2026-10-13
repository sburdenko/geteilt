using Geteilt.Contracts.Balances;
using Geteilt.Contracts.Settlements;

namespace Geteilt.Web.Settlements;

/// <summary>
/// One transfer from the plan, ready to be paid: the code to scan, and everything a person needs
/// to check it by eye or to type it into a bank that cannot read a Girocode. Everything shown
/// comes from the code's own fields, so what is read is what is scanned.
/// </summary>
/// <param name="IdempotencyKey">
/// One for every press of "paid" on this screen: a press repeated after a lost answer is the
/// same request, not a second payment.
/// </param>
public sealed record OpenTransfer(
    PlannedTransferDto Transfer,
    GirocodeDto Code,
    string IbanElectronic,
    string IbanGrouped,
    string IbanTail,
    Guid IdempotencyKey)
{
    private const int IbanGroupLength = 4;

    public string Remittance => Code.Remittance;

    /// <summary>The code as an image source. Needs <c>img-src data:</c> once a CSP is set.</summary>
    public string QrDataUri => $"data:image/png;base64,{Code.PngBase64}";

    public static OpenTransfer Create(PlannedTransferDto transfer, GirocodeDto code)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        ArgumentNullException.ThrowIfNull(code);

        string electronic = string.Concat(code.Iban.Where(character => !char.IsWhiteSpace(character)))
            .ToUpperInvariant();

        return new OpenTransfer(
            transfer,
            code,
            electronic,
            string.Join(' ', electronic.Chunk(IbanGroupLength).Select(group => new string(group))),
            electronic[^Math.Min(IbanGroupLength, electronic.Length)..],
            Guid.CreateVersion7());
    }

    /// <summary>
    /// Who pays whom, looked up by the pair rather than by position: once someone has paid, the
    /// positions shift, and the same position would name somebody else's transfer. A plan has at
    /// most one transfer per pair. Null when the pair is settled or the balances moved on.
    /// </summary>
    public static PlannedTransferDto? Find(SettlementPlanResponse plan, Guid fromMemberId, Guid toMemberId)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Transfers.FirstOrDefault(transfer =>
            transfer.FromMemberId == fromMemberId && transfer.ToMemberId == toMemberId);
    }
}
