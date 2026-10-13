using Geteilt.Web.Api;
using Geteilt.Web.Localisation;
using Geteilt.Web.Notifications;

namespace Geteilt.Web.Tests;

public sealed class LocalizerTests
{
    [Theory]
    [InlineData("de", 1, "1 Mitglied")]
    [InlineData("de", 5, "5 Mitglieder")]
    [InlineData("en", 1, "1 member")]
    [InlineData("en", 0, "0 members")]
    [InlineData("uk", 1, "1 учасник")]
    [InlineData("uk", 3, "3 учасники")]
    [InlineData("uk", 5, "5 учасників")]
    [InlineData("uk", 11, "11 учасників")]
    [InlineData("uk", 21, "21 учасник")]
    public void ACount_ReadsCorrectlyInItsLanguage(string language, int count, string expected) =>
        TestLocalizer.For(language).Plural("home.members", count).Should().Be(expected);

    [Theory]
    [InlineData("de", "12,30 €")]
    [InlineData("uk", "12,30 €")]
    public void AnAmount_IsWrittenTheWayTheLanguageWritesNumbers(string language, string expected) =>
        TestLocalizer.For(language).Money(12.30m, "EUR").Should().Be(expected);

    [Fact]
    public void TheCurrencyComesFromTheMoney_NotFromTheCulture()
    {
        // uk-UA's own currency is the hryvnia. A euro group is in euro whoever is reading.
        TestLocalizer.For("uk").Money(12.30m, "EUR").Should().Contain("€").And.NotContain("₴");
    }

    [Fact]
    public void AThousandsSeparator_FollowsTheLanguage()
    {
        TestLocalizer.For("de").Money(1234.50m, "EUR").Should().Contain("1.234,50");
        TestLocalizer.For("en").Money(1234.50m, "EUR").Should().Contain("1,234.50");
    }

    [Fact]
    public void AKeyWithNoPhrase_ShowsTheKey()
    {
        // Visible and greppable. Falling back to another language would hide the gap on a screen
        // that looks finished.
        TestLocalizer.For("de")["nothing.here"].Should().Be("nothing.here");
    }

    [Fact]
    public void AnErrorToast_UsesTheTranslationForTheCode_NotTheServersWording()
    {
        ToastService toasts = new(TestLocalizer.For("uk"));

        toasts.ShowError(new ApiError(
            401, "invalid_credentials", "Email or password is incorrect", null, NoFields));

        // The API writes one language and the reader chose another. What matters is that
        // the code decides the words, so the server's English never reaches a Ukrainian screen.
        toasts.Toasts.Single().Title.Should().Be("Неправильна пошта або пароль");
    }

    [Fact]
    public void AnErrorCodeNobodyTranslated_StillSaysSomething()
    {
        ToastService toasts = new(TestLocalizer.For("de"));

        toasts.ShowError(new ApiError(500, "teapot_on_fire", "Whatever", "diagnostics", NoFields));

        toasts.Toasts.Single().Title.Should().Be("Etwas hat nicht geklappt");
        toasts.Toasts.Single().Detail.Should().Be("diagnostics", "the server's own words are the diagnostic");
    }

    private static readonly IReadOnlyDictionary<string, string[]> NoFields = new Dictionary<string, string[]>();
}
