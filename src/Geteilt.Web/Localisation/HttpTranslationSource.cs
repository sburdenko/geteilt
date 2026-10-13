namespace Geteilt.Web.Localisation;

/// <summary>
/// Fetches <c>wwwroot/i18n/{language}.json</c>.
/// </summary>
/// <remarks>
/// One file per language, fetched when that language is chosen, rather than resource assemblies
/// compiled into the app. A reader downloads their own language and not the other two, which
/// matters on a first paint that already carries a WebAssembly runtime. It also
/// means a translator edits a JSON file instead of building the solution.
/// </remarks>
public sealed class HttpTranslationSource(HttpClient http) : ITranslationSource
{
    private readonly Dictionary<string, TranslationCatalogue> _loaded = new(StringComparer.Ordinal);

    public async Task<TranslationCatalogue> LoadAsync(string language)
    {
        if (_loaded.TryGetValue(language, out TranslationCatalogue? cached))
        {
            return cached;
        }

        string json = await http.GetStringAsync(new Uri($"i18n/{language}.json", UriKind.Relative));
        TranslationCatalogue catalogue = TranslationCatalogue.Parse(language, json);
        _loaded[language] = catalogue;

        return catalogue;
    }
}
