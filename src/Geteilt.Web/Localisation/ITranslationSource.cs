namespace Geteilt.Web.Localisation;

/// <summary>Where the phrases come from. Behind an interface so tests need no browser.</summary>
public interface ITranslationSource
{
    Task<TranslationCatalogue> LoadAsync(string language);
}

/// <summary>The chosen language, and what the browser would prefer if nothing is chosen.</summary>
public interface ILanguagePreference
{
    ValueTask<string?> ReadAsync();

    ValueTask WriteAsync(string language);

    ValueTask<IReadOnlyList<string>> BrowserPreferencesAsync();
}
