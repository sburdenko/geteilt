using Geteilt.Web.Localisation;

namespace Geteilt.Web.Tests;

/// <summary>
/// A localiser over the shipped translation files, so tests assert the phrases people actually
/// read rather than fixtures that can drift away from them.
/// </summary>
internal static class TestLocalizer
{
    internal static Localizer For(string language)
    {
        StaticTranslations source = new(language);
        LanguageState state = new(source, new NoPreference(language));
        state.LoadAsync().GetAwaiter().GetResult();

        return new Localizer(state);
    }

    internal static LanguageState StateFor(string language)
    {
        LanguageState state = new(new StaticTranslations(language), new NoPreference(language));
        state.LoadAsync().GetAwaiter().GetResult();

        return state;
    }

    private sealed class StaticTranslations(string only) : ITranslationSource
    {
        public Task<TranslationCatalogue> LoadAsync(string language) =>
            Task.FromResult(TranslationCatalogue.Parse(language, Files.Read(language == only ? language : language)));
    }

    private sealed class NoPreference(string language) : ILanguagePreference
    {
        public ValueTask<string?> ReadAsync() => ValueTask.FromResult<string?>(language);

        public ValueTask WriteAsync(string value) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<string>> BrowserPreferencesAsync() =>
            ValueTask.FromResult<IReadOnlyList<string>>([language]);
    }
}

internal static class Files
{
    internal static string Read(string language) =>
        File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "Geteilt.Web", "wwwroot", "i18n", $"{language}.json"));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Geteilt.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Geteilt.sln not found.");
    }
}
