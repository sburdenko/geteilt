using Geteilt.Web.Api;
using Geteilt.Web.Localisation;

namespace Geteilt.Web.Notifications;

/// <summary>
/// The words for an error, chosen by its <c>code</c> rather than the server's <c>title</c>. The API
/// writes its titles in one language, and the person reading the screen chose another.
/// An unrecognised code still says something.
/// </summary>
public static class ErrorPhrase
{
    public static string For(Localizer localizer, ApiError error)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(error);

        string key = $"error.{error.Code}";
        string phrase = localizer[key];

        return phrase == key ? localizer["error.unknown"] : phrase;
    }
}
