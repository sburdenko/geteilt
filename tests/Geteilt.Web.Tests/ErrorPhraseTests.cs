using Geteilt.Web.Api;
using Geteilt.Web.Notifications;

namespace Geteilt.Web.Tests;

/// <summary>The words for an error come from its code, wherever it is shown.</summary>
public sealed class ErrorPhraseTests
{
    [Fact]
    public void AKnownCode_IsSaidInTheReadersLanguage()
    {
        ErrorPhrase.For(TestLocalizer.For("en"), ApiError.Unreachable("refused"))
            .Should().Be("No connection to the server");
    }

    [Fact]
    public void AnUnknownCode_StillSaysSomething()
    {
        ApiError error = new(500, "code_nobody_has_seen", "Boom", null, new Dictionary<string, string[]>());

        ErrorPhrase.For(TestLocalizer.For("en"), error).Should().Be("Something did not work");
    }
}
