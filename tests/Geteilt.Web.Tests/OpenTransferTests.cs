using Geteilt.Contracts.Balances;
using Geteilt.Contracts.Settlements;
using Geteilt.Web.Settlements;

namespace Geteilt.Web.Tests;

/// <summary>What the QR screen shows under the code, for the eye and for a bank without Girocode.</summary>
public sealed class OpenTransferTests
{
    private static readonly Guid Ben = Guid.Parse("0198f000-0000-7000-8000-0000000000b2");
    private static readonly Guid Cleo = Guid.Parse("0198f000-0000-7000-8000-0000000000c3");
    private static readonly Guid Anna = Guid.Parse("0198f000-0000-7000-8000-0000000000a1");
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static readonly PlannedTransferDto BenPaysAnna =
        new(Ben, "Ben", Anna, "Anna", 3.33m, "EUR", "DE89370400440532013000", CanGenerateQr: true);

    private static readonly GirocodeDto Code = new(
        "Anna", "DE89370400440532013000", 3.33m, "EUR", "Geteilt: Kaffee", Convert.ToBase64String([0x89, 0x50]));

    [Fact]
    public void TheIban_IsGroupedInFoursForReading()
    {
        OpenTransfer.Create(BenPaysAnna, Code).IbanGrouped.Should().Be("DE89 3704 0044 0532 0130 00");
    }

    [Fact]
    public void TheLastFourCharacters_LetThePayerCheckWhomTheyPay()
    {
        OpenTransfer.Create(BenPaysAnna, Code).IbanTail.Should().Be("3000");
    }

    [Fact]
    public void TheCopiedIban_HasNoSpaces()
    {
        // Every banking app accepts the electronic form; not every one strips pasted spaces.
        OpenTransfer.Create(BenPaysAnna, Code with { Iban = "de89 3704 0044 0532 0130 00" })
            .IbanElectronic.Should().Be("DE89370400440532013000");
    }

    [Fact]
    public void WhatIsShown_IsWhatIsInsideTheCode()
    {
        // The plan was read a moment earlier and may name an older IBAN; the code's own fields win.
        OpenTransfer open = OpenTransfer.Create(BenPaysAnna with { ToIban = "DE02120300000000202051" }, Code);

        open.IbanElectronic.Should().Be(Code.Iban);
        open.Remittance.Should().Be(Code.Remittance);
        open.QrDataUri.Should().Be($"data:image/png;base64,{Code.PngBase64}");
    }

    [Fact]
    public void EachOpenedTransfer_HasItsOwnKey_AndKeepsIt()
    {
        // One key for every press of "paid" on this screen: a retry after a lost answer is then
        // the same request, not a second payment.
        OpenTransfer open = OpenTransfer.Create(BenPaysAnna, Code);

        open.IdempotencyKey.Should().NotBe(Guid.Empty);
        open.IdempotencyKey.Should().Be(open.IdempotencyKey);
        OpenTransfer.Create(BenPaysAnna, Code).IdempotencyKey.Should().NotBe(open.IdempotencyKey);
    }

    [Fact]
    public void Find_LooksUpWhoPaysWhom()
    {
        OpenTransfer.Find(Plan(), Cleo, Anna)?.FromName.Should().Be("Cleo");
    }

    [Fact]
    public void Find_ForAPairNoLongerInThePlan_FindsNothing()
    {
        // Ben paid; going back to his screen must not land on Cleo's transfer instead.
        SettlementPlanResponse afterBenPaid = new("EUR", [CleoPaysAnna()], Now);

        OpenTransfer.Find(afterBenPaid, Ben, Anna).Should().BeNull();
    }

    private static PlannedTransferDto CleoPaysAnna() => BenPaysAnna with { FromMemberId = Cleo, FromName = "Cleo" };

    private static SettlementPlanResponse Plan() => new("EUR", [BenPaysAnna, CleoPaysAnna()], Now);
}
