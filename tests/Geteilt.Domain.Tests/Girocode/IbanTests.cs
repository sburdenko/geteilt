using Geteilt.Domain.Girocode;

namespace Geteilt.Domain.Tests.Girocode;

/// <summary>IBAN validation: country length, characters, mod-97.</summary>
public sealed class IbanTests
{
    public static TheoryData<string> ValidFromReferenceTable() => new()
    {
        "DE89370400440532013000",
        "DE02120300000000202051",
        "DE71110220330123456789",
        "AT611904300234573201",
        "NL91ABNA0417164300",
        "DE89 3704 0044 0532 0130 00",
        "de89370400440532013000",
    };

    public static TheoryData<string> InvalidFromReferenceTable() => new()
    {
        "DE89370400440532013001",  // check digits do not match
        "DE8937040044053201300",   // 21 characters, DE is 22
        "XX89370400440532013000",  // unknown country
        "",
    };

    /// <summary>One valid IBAN per country the length table has to know.</summary>
    public static TheoryData<string, int> LengthTable() => new()
    {
        { "DE89370400440532013000", 22 },
        { "AT611904300234573201", 20 },
        { "CH9300762011623852957", 21 },
        { "FR1420041010050500013M02606", 27 },
        { "NL91ABNA0417164300", 18 },
        { "IT60X0542811101000000123456", 27 },
        { "ES9121000418450200051332", 24 },
        { "BE68539007547034", 16 },
        { "PL61109010140000071219812874", 28 },
    };

    [Theory]
    [MemberData(nameof(ValidFromReferenceTable))]
    public void IsValid_ForKnownGoodIban_ReturnsTrue(string iban) =>
        Iban.IsValid(iban).Should().BeTrue();

    [Theory]
    [MemberData(nameof(InvalidFromReferenceTable))]
    public void IsValid_ForKnownBadIban_ReturnsFalse(string iban) =>
        Iban.IsValid(iban).Should().BeFalse();

    [Theory]
    [MemberData(nameof(LengthTable))]
    public void IsValid_ForEachSupportedCountry_AcceptsTheCountryLength(string iban, int expectedLength)
    {
        iban.Length.Should().Be(expectedLength);

        Iban.IsValid(iban).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(LengthTable))]
    public void IsValid_WhenLengthIsWrongForTheCountry_ReturnsFalse(string iban, int expectedLength)
    {
        _ = expectedLength;

        Iban.IsValid(iban[..^1]).Should().BeFalse();
        Iban.IsValid(iban + "0").Should().BeFalse();
    }

    [Fact]
    public void IsValid_IgnoresSpacesAndCase()
    {
        Iban.IsValid("  de89 3704 0044 0532 0130 00  ").Should().BeTrue();
    }

    public static TheoryData<string?> Garbage() => new()
    {
        null,
        " ",
        "not an iban at all",
        "DE",
        "1234567890123456",
        "DE89-3704-0044-0532-0130-00",
        "DE8937040044053201300O",
        "ДЕ89370400440532013000",
        "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0",
    };

    [Theory]
    [MemberData(nameof(Garbage))]
    public void IsValid_OnGarbage_ReturnsFalseAndDoesNotThrow(string? input)
    {
        Func<bool> act = () => Iban.IsValid(input);

        act.Should().NotThrow().Which.Should().BeFalse();
    }

    [Fact]
    public void IsValid_OnAbsurdlyLongInput_ReturnsFalse()
    {
        Iban.IsValid(new string('1', 10_000)).Should().BeFalse();
    }
}
