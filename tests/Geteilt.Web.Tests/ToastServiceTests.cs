using Geteilt.Web.Api;
using Geteilt.Web.Notifications;

namespace Geteilt.Web.Tests;

/// <summary>The toast shows `title`, with `detail` behind a disclosure.</summary>
public sealed class ToastServiceTests
{
    private readonly ToastService _toasts = new(TestLocalizer.For("de"));

    [Fact]
    public void AnApiError_BecomesAToastTitledByTheProblemDocument()
    {
        _toasts.ShowError(new ApiError(
            409, "email_already_registered", "Diese E-Mail ist schon registriert", "Andere Adresse wählen.", NoFields));

        Toast toast = _toasts.Toasts.Should().ContainSingle().Subject;
        toast.Title.Should().Be("Diese E-Mail ist schon registriert");
        toast.Detail.Should().Be("Andere Adresse wählen.");
        toast.Level.Should().Be(ToastLevel.Error);
    }

    [Fact]
    public void ADetailThatOnlyRepeatsTheTitle_IsNotShownTwice()
    {
        // The auth endpoints send exactly this, deliberately, so as not to leak which half was wrong.
        _toasts.ShowError(new ApiError(
            401, "invalid_credentials", "E-Mail oder Passwort ist falsch", "E-Mail oder Passwort ist falsch", NoFields));

        _toasts.Toasts.Single().Detail.Should().BeNull();
    }

    [Fact]
    public void FieldErrors_AreListedInTheDetail()
    {
        _toasts.ShowError(new ApiError(
            422, "validation_failed", "Die Eingaben sind unvollständig", null,
            new Dictionary<string, string[]> { ["password"] = ["Zu kurz", "Keine Ziffer"] }));

        _toasts.Toasts.Single().Detail.Should().Contain("Zu kurz").And.Contain("Keine Ziffer");
    }

    [Fact]
    public void AnErrorStaysUntilItIsDismissed()
    {
        _toasts.ShowError(new ApiError(500, "server_error", "Serverfehler", null, NoFields));
        Guid id = _toasts.Toasts.Single().Id;

        _toasts.Dismiss(id);

        _toasts.Toasts.Should().BeEmpty();
    }

    private static readonly IReadOnlyDictionary<string, string[]> NoFields = new Dictionary<string, string[]>();
}
