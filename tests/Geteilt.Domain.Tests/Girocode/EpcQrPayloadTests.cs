using System.Globalization;
using System.Text;
using Geteilt.Domain.Girocode;

namespace Geteilt.Domain.Tests.Girocode;

/// <summary>The EPC069-12 payload. The reference payloads are compared byte for byte.</summary>
public sealed class EpcQrPayloadTests
{
    private static GirocodeRequest ReferenceOne() => new()
    {
        BeneficiaryName = "Franz Mustermänn",
        Iban = "DE71110220330123456789",
        Amount = 12.30m,
        Currency = "EUR",
        Remittance = "Abendessen Ristorante",
    };

    private static GirocodeRequest ReferenceTwo() => new()
    {
        BeneficiaryName = "Anna Weber",
        Iban = "DE02120300000000202051",
        Amount = 5.00m,
        Currency = "EUR",
    };

    [Fact]
    public void Build_MatchesReferencePayloadOne()
    {
        const string expected =
            "BCD\n" +
            "002\n" +
            "1\n" +
            "SCT\n" +
            "\n" +
            "Franz Mustermänn\n" +
            "DE71110220330123456789\n" +
            "EUR12.30\n" +
            "\n" +
            "\n" +
            "Abendessen Ristorante";

        string payload = EpcQrPayload.Build(ReferenceOne());

        payload.Should().Be(expected);
        Encoding.UTF8.GetBytes(payload).Should().Equal(Encoding.UTF8.GetBytes(expected));
    }

    [Fact]
    public void Build_MatchesReferencePayloadTwo_WithTrailingEmptyLinesDropped()
    {
        const string expected =
            "BCD\n" +
            "002\n" +
            "1\n" +
            "SCT\n" +
            "\n" +
            "Anna Weber\n" +
            "DE02120300000000202051\n" +
            "EUR5.00";

        string payload = EpcQrPayload.Build(ReferenceTwo());

        payload.Should().Be(expected);
        payload.Should().NotEndWith("\n");
    }

    [Fact]
    public void Build_SeparatesLinesWithLineFeedOnly()
    {
        EpcQrPayload.Build(ReferenceOne()).Should().NotContain("\r");
    }

    [Fact]
    public void Build_FormatsAmountWithInvariantCulture_EvenUnderGermanCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            EpcQrPayload.Build(ReferenceOne()).Should().Contain("EUR12.30").And.NotContain("EUR12,30");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Build_TruncatesBeneficiaryNameToSeventyCharacters()
    {
        string payload = EpcQrPayload.Build(ReferenceOne() with { BeneficiaryName = new string('A', 100) });

        payload.Split('\n')[5].Should().HaveLength(EpcQrPayload.MaxBeneficiaryNameLength);
    }

    [Fact]
    public void Build_TruncatesRemittanceToOneHundredFortyBytes()
    {
        string payload = EpcQrPayload.Build(ReferenceOne() with { Remittance = new string('A', 200) });

        string remittance = payload.Split('\n')[10];
        Encoding.UTF8.GetByteCount(remittance).Should().Be(EpcQrPayload.MaxRemittanceBytes);
    }

    [Fact]
    public void Build_CountsRemittanceInBytesNotCharacters_AndNeverSplitsACharacter()
    {
        // 'ä' is two bytes, so 140 bytes is only 70 of them and the cut must land between characters.
        string payload = EpcQrPayload.Build(ReferenceOne() with { Remittance = new string('ä', 100) });

        string remittance = payload.Split('\n')[10];

        remittance.Should().HaveLength(70);
        Encoding.UTF8.GetByteCount(remittance).Should().Be(EpcQrPayload.MaxRemittanceBytes);
        remittance.Should().NotContain("�");
    }

    [Fact]
    public void RemittanceFor_ALongUkrainianGroupName_StopsAtAWholeLetter()
    {
        // After the 9-byte "Geteilt: " prefix 131 bytes are left, an odd number:
        // 65 two-byte letters fit and the 66th would be split in half, so it is dropped whole.
        // No spaces in the name on purpose — a one-byte space would change the parity.
        string name = string.Concat(Enumerable.Repeat("ПоїздкаЛьвів", 8));

        string remittance = EpcQrPayload.RemittanceFor(name);

        remittance.Should().StartWith("Geteilt: Поїздка");
        remittance.Length.Should().Be("Geteilt: ".Length + 65);
        Encoding.UTF8.GetByteCount(remittance).Should().Be(EpcQrPayload.MaxRemittanceBytes - 1);
        remittance.Should().NotContain("\uFFFD");
        name.Should().StartWith(remittance["Geteilt: ".Length..]);
    }

    [Fact]
    public void RemittanceFor_AShortUkrainianGroupName_IsKeptWhole()
    {
        EpcQrPayload.RemittanceFor("Вечеря в Києві").Should().Be("Geteilt: Вечеря в Києві");
    }

    [Fact]
    public void RemittanceFor_AFourByteCharacterAtTheCut_IsDroppedWhole_NotHalved()
    {
        // An emoji is two UTF-16 units and four UTF-8 bytes; cutting by chars would leave half of it.
        string name = new string('a', 129) + "🍕";

        string remittance = EpcQrPayload.RemittanceFor(name);

        Encoding.UTF8.GetByteCount(remittance).Should().Be("Geteilt: ".Length + 129);
        char.IsSurrogate(remittance[^1]).Should().BeFalse();
    }

    [Theory]
    [InlineData("Müller")]
    [InlineData("Straße")]
    [InlineData("Mustermänn")]
    public void Build_KeepsGermanCharactersIntact(string name)
    {
        string payload = EpcQrPayload.Build(ReferenceOne() with { BeneficiaryName = name });

        payload.Split('\n')[5].Should().Be(name);
    }

    [Fact]
    public void Build_WhenCurrencyIsNotEuro_Throws()
    {
        Action act = () => EpcQrPayload.Build(ReferenceOne() with { Currency = "USD" });

        act.Should().ThrowExactly<EpcQrException>().Which.Code.Should().Be("epc_only_eur");
    }

    public static TheoryData<decimal> AmountsOutOfRange() => new() { 0m, 0.009m, -1m, 1_000_000_000.00m };

    [Theory]
    [MemberData(nameof(AmountsOutOfRange))]
    public void Build_WhenAmountIsOutOfRange_Throws(decimal amount)
    {
        Action act = () => EpcQrPayload.Build(ReferenceOne() with { Amount = amount });

        act.Should().ThrowExactly<EpcQrException>().Which.Code.Should().Be("epc_amount_out_of_range");
    }

    public static TheoryData<decimal> AmountsAtTheEdge() => new() { 0.01m, 999_999_999.99m };

    [Theory]
    [MemberData(nameof(AmountsAtTheEdge))]
    public void Build_AcceptsTheBoundaryAmounts(decimal amount)
    {
        Action act = () => EpcQrPayload.Build(ReferenceOne() with { Amount = amount });

        act.Should().NotThrow();
    }

    [Fact]
    public void Build_WhenIbanIsInvalid_Throws()
    {
        Action act = () => EpcQrPayload.Build(ReferenceOne() with { Iban = "DE89370400440532013001" });

        act.Should().ThrowExactly<EpcQrException>().Which.Code.Should().Be("invalid_iban");
    }

    [Fact]
    public void Build_WhenIbanIsMissing_Throws()
    {
        Action act = () => EpcQrPayload.Build(ReferenceOne() with { Iban = "  " });

        act.Should().ThrowExactly<EpcQrException>().Which.Code.Should().Be("epc_no_iban");
    }

    [Fact]
    public void Build_NormalisesTheIban()
    {
        string payload = EpcQrPayload.Build(ReferenceOne() with { Iban = "de71 1102 2033 0123 456789" });

        payload.Split('\n')[6].Should().Be("DE71110220330123456789");
    }

    [Fact]
    public void Build_StaysWithinTheThreeHundredThirtyOneByteLimit()
    {
        string payload = EpcQrPayload.Build(ReferenceOne() with
        {
            BeneficiaryName = new string('Ä', 70),
            Remittance = new string('Ö', 70),
        });

        Encoding.UTF8.GetByteCount(payload).Should().BeLessThanOrEqualTo(EpcQrPayload.MaxPayloadBytes);
    }

    [Fact]
    public void RemittanceFor_UsesTheGroupNameBehindTheApplicationPrefix()
    {
        EpcQrPayload.RemittanceFor("Italien 2026").Should().Be("Geteilt: Italien 2026");
    }

    [Fact]
    public void RemittanceFor_WhenGroupNameIsLong_KeepsThePrefixAndTrimsTheName()
    {
        string remittance = EpcQrPayload.RemittanceFor(new string('A', 300));

        remittance.Should().StartWith("Geteilt: ");
        Encoding.UTF8.GetByteCount(remittance).Should().Be(EpcQrPayload.MaxRemittanceBytes);
    }
}
