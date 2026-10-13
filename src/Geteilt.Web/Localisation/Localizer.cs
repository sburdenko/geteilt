using System.Globalization;

namespace Geteilt.Web.Localisation;

/// <summary>
/// Text in the language the person chose.
/// </summary>
/// <remarks>
/// Not <c>IStringLocalizer</c>: it has no notion of plural forms, and Ukrainian needs four of them
///. Rather than two mechanisms — the platform's for plain strings and something else for
/// counts — there is one, and it reads the same either way.
/// </remarks>
public sealed class Localizer(LanguageState language)
{
    public string this[string key] => Resolve(key, PluralCategory.Other);

    public string Format(string key, params object?[] arguments) =>
        string.Format(language.Culture, Resolve(key, PluralCategory.Other), arguments);

    /// <summary>
    /// The phrase for <paramref name="count"/>, with the count itself as <c>{0}</c>. The number is
    /// formatted in the current culture, so German gets "1.234" where English gets "1,234".
    /// </summary>
    public string Plural(string key, decimal count, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        PluralCategory category = PluralRules.For(language.Current, count);
        object?[] all = [count, .. arguments];

        return string.Format(language.Culture, Resolve(key, category), all);
    }

    private string Resolve(string key, PluralCategory category) =>
        language.Catalogue.TryGet(key, out Phrase phrase)
            ? phrase.For(category)
            // Showing the key is deliberate: it is obviously wrong on screen and obviously
            // greppable, where falling back to another language hides the gap. The catalogue test
            // is what stops one reaching a person at all.
            : key;

    /// <summary>
    /// An amount written the way the current language writes numbers, in the group's currency.
    /// </summary>
    /// <remarks>
    /// The culture's own currency is not the answer: uk-UA would print hryvnia for a euro group.
    /// The pattern comes from the language — where the symbol sits, comma or point — and the
    /// symbol comes from the money.
    /// </remarks>
    public string Money(decimal amount, string currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        NumberFormatInfo format = (NumberFormatInfo)language.Culture.NumberFormat.Clone();
        format.CurrencySymbol = Symbols.GetValueOrDefault(currency, currency);

        return amount.ToString("C", format);
    }

    private static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
    {
        ["EUR"] = "€",
    };
}
