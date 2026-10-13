namespace Geteilt.Domain.Tests;

/// <summary>Currency codes: the ECB set, upper case, three letters.</summary>
public sealed class CurrencyTests
{
    [Theory]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("CHF")]
    [InlineData("PLN")]
    [InlineData("GBP")]
    public void IsValid_ForSupportedCode_ReturnsTrue(string code) =>
        Currency.IsValid(code).Should().BeTrue();

    [Theory]
    [InlineData("eur")]
    [InlineData("Eur")]
    [InlineData(" chf ")]
    public void IsValid_NormalisesCaseAndSurroundingSpace(string code) =>
        Currency.IsValid(code).Should().BeTrue();

    [Theory]
    [InlineData("EURO")]
    [InlineData("E")]
    [InlineData("123")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XYZ")]
    [InlineData("EU R")]
    [InlineData(null)]
    public void IsValid_ForAnythingElse_ReturnsFalse(string? code) =>
        Currency.IsValid(code).Should().BeFalse();

    [Fact]
    public void Normalise_ReturnsUpperCaseCode()
    {
        Currency.Normalise(" eur ").Should().Be("EUR");
    }

    [Fact]
    public void Normalise_OnInvalidCode_Throws()
    {
        Action act = () => Currency.Normalise("EURO");

        act.Should().ThrowExactly<ArgumentException>().WithParameterName("code");
    }

    [Fact]
    public void Base_IsEuro()
    {
        Currency.Euro.Should().Be("EUR");
        Currency.IsValid(Currency.Euro).Should().BeTrue();
    }
}
