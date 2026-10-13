using System.Globalization;

namespace Geteilt.Web.Localisation;

/// <summary>
/// The language the app is in, and the phrases for it.
/// </summary>
/// <remarks>
/// Singleton for the same reason <c>AuthState</c> is: one browser tab has one reader, and a scoped
/// registration would hand a different instance to anything the HTTP client factory builds.
/// </remarks>
public sealed class LanguageState(ITranslationSource translations, ILanguagePreference preference)
{
    private TranslationCatalogue? _catalogue;

    public string Current { get; private set; } = SupportedLanguage.Fallback;

    /// <summary>
    /// The culture used for numbers and dates. English maps to Ireland rather than the United
    /// States or Britain: it is the English that writes euro amounts and day-before-month dates,
    /// which is what a reader of this app is looking at.
    /// </summary>
    public CultureInfo Culture { get; private set; } = CultureFor(SupportedLanguage.Fallback);

    public TranslationCatalogue Catalogue =>
        _catalogue ?? throw new InvalidOperationException(
            $"{nameof(LoadAsync)} has to run before the first render.");

    public event Action? Changed;

    /// <summary>Picks the language and loads it, before anything renders.</summary>
    public async Task LoadAsync()
    {
        string chosen = await preference.ReadAsync() is { } stored && SupportedLanguage.IsSupported(stored)
            ? stored
            : SupportedLanguage.BestMatch(await preference.BrowserPreferencesAsync());

        await ApplyAsync(chosen, remember: false);
    }

    public Task SetAsync(string language)
    {
        if (!SupportedLanguage.IsSupported(language))
        {
            throw new ArgumentException($"'{language}' is not one of the supported languages.", nameof(language));
        }

        return ApplyAsync(language, remember: true);
    }

    private async Task ApplyAsync(string language, bool remember)
    {
        _catalogue = await translations.LoadAsync(language);
        Current = language;
        Culture = CultureFor(language);

        // Set on the thread as well, so anything formatting through the ambient culture — the
        // framework's own validation messages, a stray ToString() — agrees with the rest.
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;

        if (remember)
        {
            await preference.WriteAsync(language);
        }

        Changed?.Invoke();
    }

    private static CultureInfo CultureFor(string language) => language switch
    {
        SupportedLanguage.German => new CultureInfo("de-DE"),
        SupportedLanguage.English => new CultureInfo("en-IE"),
        SupportedLanguage.Ukrainian => new CultureInfo("uk-UA"),
        _ => new CultureInfo("de-DE"),
    };
}
