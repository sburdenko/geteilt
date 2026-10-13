using System.Text.RegularExpressions;
using Geteilt.Web.Localisation;

namespace Geteilt.Web.Tests;

/// <summary>
/// Reads the shipped translation files. A missing phrase is the kind of thing nobody notices until
/// somebody who reads that language opens the app, so it fails the build instead.
/// </summary>
public sealed partial class TranslationCatalogueTests
{
    private static readonly Dictionary<string, TranslationCatalogue> Catalogues =
        SupportedLanguage.All.ToDictionary(
            language => language,
            language => TranslationCatalogue.Parse(language, ReadFile(language)),
            StringComparer.Ordinal);

    [Fact]
    public void EverySupportedLanguage_HasAFileAndAPluralRule()
    {
        foreach (string language in SupportedLanguage.All)
        {
            Catalogues.Should().ContainKey(language);
            PluralRules.Supports(language).Should().BeTrue(
                "shipping {0} without its plural rule would throw at the first count on screen",
                language);
        }
    }

    [Fact]
    public void EveryLanguage_HasTheSameKeys()
    {
        IReadOnlyCollection<string> reference = Catalogues[SupportedLanguage.German].Keys;

        foreach ((string language, TranslationCatalogue catalogue) in Catalogues)
        {
            catalogue.Keys.Should().BeEquivalentTo(
                reference, "'{0}' has to say everything German says", language);
        }
    }

    [Theory]
    [InlineData("de", new[] { PluralCategory.One, PluralCategory.Other })]
    [InlineData("en", new[] { PluralCategory.One, PluralCategory.Other })]
    [InlineData("uk", new[] { PluralCategory.One, PluralCategory.Few, PluralCategory.Many, PluralCategory.Other })]
    public void APluralPhrase_CarriesEveryFormItsLanguageUses(string language, PluralCategory[] required)
    {
        TranslationCatalogue catalogue = Catalogues[language];

        foreach (string key in PluralKeys())
        {
            catalogue.TryGet(key, out Phrase phrase).Should().BeTrue();

            Phrase.Plural plural = phrase.Should().BeOfType<Phrase.Plural>().Subject;
            plural.Forms.Keys.Should().BeEquivalentTo(
                required, "'{0}' in {1} needs exactly the forms {1} uses", key, language);
        }
    }

    [Fact]
    public void EveryFormOfEveryTranslation_KeepsThePlaceholdersTheOriginalHas()
    {
        TranslationCatalogue german = Catalogues[SupportedLanguage.German];

        foreach (string key in german.Keys)
        {
            german.TryGet(key, out Phrase reference);
            HashSet<string> expected = FormsOf(reference).SelectMany(Placeholders).ToHashSet(StringComparer.Ordinal);

            foreach ((string language, TranslationCatalogue catalogue) in Catalogues)
            {
                catalogue.TryGet(key, out Phrase phrase);

                // Per form, not pooled across them. A translator who drops {0} from just the "few"
                // form produces "учасники" with no number in front, and pooling would hide it
                // behind the forms that still have one.
                foreach (string form in FormsOf(phrase))
                {
                    Placeholders(form).Should().BeEquivalentTo(
                        expected, "'{0}' in {1} keeps its placeholders in every form", key, language);
                }
            }
        }
    }

    [Fact]
    public void EveryErrorCodeTheClientCanProduce_HasAPhrase()
    {
        TranslationCatalogue german = Catalogues[SupportedLanguage.German];

        foreach (string code in new[] { "network_unreachable", "response_unreadable", "unknown" })
        {
            german.TryGet($"error.{code}", out _).Should().BeTrue();
        }
    }

    private static IEnumerable<string> PluralKeys() => ["home.members"];

    private static IEnumerable<string> FormsOf(Phrase phrase) => phrase switch
    {
        Phrase.Simple simple => [simple.Text],
        Phrase.Plural plural => plural.Forms.Values,
        _ => [],
    };

    private static HashSet<string> Placeholders(string text) =>
        [.. PlaceholderPattern().Matches(text).Select(match => match.Value)];

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();

    private static string ReadFile(string language) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Geteilt.Web", "wwwroot", "i18n", $"{language}.json"));

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
