using Geteilt.Web.Localisation;

namespace Geteilt.Web.Tests;

/// <summary>
/// The examples come from the CLDR plural rules for each language, not from what looked right.
/// </summary>
public sealed class PluralRulesTests
{
    [Theory]
    [InlineData(1)]
    public void English_UsesTheSingularOnlyForExactlyOne(int count) =>
        PluralRules.For("en", count).Should().Be(PluralCategory.One);

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(11)]
    [InlineData(21)]
    [InlineData(100)]
    public void English_UsesThePluralForEverythingElse(int count) =>
        PluralRules.For("en", count).Should().Be(PluralCategory.Other);

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(21)]
    public void German_FollowsTheSameTwoFormsAsEnglish(int count) =>
        PluralRules.For("de", count).Should().Be(PluralRules.For("en", count));

    [Theory]
    [InlineData(1)]
    [InlineData(21)]
    [InlineData(31)]
    [InlineData(101)]
    [InlineData(1001)]
    public void Ukrainian_One(int count) =>
        PluralRules.For("uk", count).Should().Be(PluralCategory.One);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(22)]
    [InlineData(34)]
    [InlineData(102)]
    public void Ukrainian_Few(int count) =>
        PluralRules.For("uk", count).Should().Be(PluralCategory.Few);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(19)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void Ukrainian_Many(int count) =>
        PluralRules.For("uk", count).Should().Be(PluralCategory.Many);

    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    [InlineData("de")]
    public void AWrittenFraction_TakesTheOtherForm(string language)
    {
        // CLDR's `v`: 1.0 has a fraction digit written down and 1 does not, and every one of these
        // languages treats the two differently. "1,0 Ausgaben", not "1,0 Ausgabe".
        PluralRules.For(language, 1.0m).Should().Be(PluralCategory.Other);
        PluralRules.For(language, 1m).Should().NotBe(PluralCategory.Other);
    }

    [Fact]
    public void ANegativeCount_TakesTheFormOfItsMagnitude()
    {
        // Balances go negative and are pluralised the same way as the amount owed.
        PluralRules.For("uk", -2).Should().Be(PluralCategory.Few);
        PluralRules.For("en", -1).Should().Be(PluralCategory.One);
    }

    [Fact]
    public void ALanguageWithNoRule_IsRefusedRatherThanGuessed()
    {
        // Falling back to English would hand Ukrainian two forms where it needs four, and the
        // result reads as broken to anyone who speaks it — while every test still passed.
        PluralRules.Supports("pl").Should().BeFalse();

        Action guessing = () => PluralRules.For("pl", 2);

        guessing.Should().Throw<NotSupportedException>();
    }
}
