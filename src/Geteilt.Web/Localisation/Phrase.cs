using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geteilt.Web.Localisation;

/// <summary>
/// One entry in a translation file: either a single string, or one string per plural form.
/// Closed, so reading an entry cannot forget that plurals exist.
/// </summary>
[JsonConverter(typeof(PhraseConverter))]
public abstract record Phrase
{
    private Phrase()
    {
    }

    public sealed record Simple(string Text) : Phrase;

    public sealed record Plural(IReadOnlyDictionary<PluralCategory, string> Forms) : Phrase;

    /// <summary>
    /// The text for a count. A simple phrase ignores the count — that is what makes a missing
    /// plural entry visible in the tests rather than at a customer.
    /// </summary>
    public string For(PluralCategory category) => this switch
    {
        Simple simple => simple.Text,
        Plural plural => plural.Forms.TryGetValue(category, out string? text)
            ? text
            // A language whose file omits a form it needs is a bug the catalogue test catches.
            // At runtime the other form is still better than an empty screen.
            : plural.Forms.GetValueOrDefault(PluralCategory.Other, string.Empty),
        _ => throw new UnreachableException(),
    };
}

internal sealed class PhraseConverter : JsonConverter<Phrase>
{
    public override Phrase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new Phrase.Simple(reader.GetString() ?? string.Empty);
        }

        Dictionary<PluralCategory, string> forms = [];

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            string name = reader.GetString() ?? string.Empty;
            reader.Read();

            if (!Enum.TryParse(name, ignoreCase: true, out PluralCategory category))
            {
                throw new JsonException($"'{name}' is not a CLDR plural category.");
            }

            forms[category] = reader.GetString() ?? string.Empty;
        }

        return new Phrase.Plural(forms);
    }

    public override void Write(Utf8JsonWriter writer, Phrase value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Translation files are written by hand, not by the app.");
}
