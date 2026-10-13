using System.Text.Json;

namespace Geteilt.Web.Localisation;

/// <summary>Every phrase for one language.</summary>
public sealed class TranslationCatalogue(string language, IReadOnlyDictionary<string, Phrase> phrases)
{
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    public string Language { get; } = language;

    public IReadOnlyCollection<string> Keys { get; } = [.. phrases.Keys];

    public static TranslationCatalogue Parse(string language, string json)
    {
        Dictionary<string, Phrase> phrases =
            JsonSerializer.Deserialize<Dictionary<string, Phrase>>(json, ReadOptions)
            ?? throw new JsonException($"The translation file for '{language}' is empty.");

        return new TranslationCatalogue(language, phrases);
    }

    public bool TryGet(string key, out Phrase phrase) => phrases.TryGetValue(key, out phrase!);
}
