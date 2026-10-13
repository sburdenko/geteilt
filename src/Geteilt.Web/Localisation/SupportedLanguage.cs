namespace Geteilt.Web.Localisation;

/// <summary>The languages the product ships in.</summary>
public static class SupportedLanguage
{
    public const string German = "de";
    public const string English = "en";
    public const string Ukrainian = "uk";

    /// <summary>German first: it is the market the product is built for.</summary>
    public static IReadOnlyList<string> All { get; } = [German, English, Ukrainian];

    public static string Fallback => German;

    public static bool IsSupported(string? language) =>
        language is not null && All.Contains(language, StringComparer.Ordinal);

    /// <summary>
    /// The best match for what the browser asks for. <c>navigator.languages</c> gives entries like
    /// "uk-UA" and "de-AT", so the region is dropped before matching — a German speaker in Austria
    /// wants German, not the fallback.
    /// </summary>
    public static string BestMatch(IEnumerable<string> preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        foreach (string preference in preferences)
        {
            string language = preference.Split('-')[0].ToLowerInvariant();

            if (IsSupported(language))
            {
                return language;
            }
        }

        return Fallback;
    }
}
