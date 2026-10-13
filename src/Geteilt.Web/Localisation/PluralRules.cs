namespace Geteilt.Web.Localisation;

/// <summary>
/// Which plural form a language uses for a given number, per the CLDR plural rules.
/// </summary>
/// <remarks>
/// Hand-written rather than taken from a library because .NET ships no plural rules at all —
/// <c>IStringLocalizer</c> has no concept of them — and the three languages here are specified in
/// a dozen lines. The boundary is explicit: a fourth language must add its rule here, and
/// <see cref="For"/> throws rather than guessing. Guessing would mean silently giving Ukrainian
/// English's two forms, which reads as broken to anyone who speaks it.
/// </remarks>
public static class PluralRules
{
    public static bool Supports(string language) =>
        language is "de" or "en" or "uk";

    public static PluralCategory For(string language, decimal count)
    {
        ArgumentNullException.ThrowIfNull(language);

        // CLDR counts by the absolute value: minus one member is not a thing, but a negative
        // balance is, and "−1 €" takes the same form as "1 €".
        decimal absolute = Math.Abs(count);
        long integerPart = (long)decimal.Truncate(absolute);

        // CLDR's `v`: the number of fraction digits actually written, which decimal keeps as its
        // scale. This is why "1 Ausgabe" but "1,00 €" — with digits after the comma English and
        // German both leave the singular, and 1.0m carries that scale where 1m does not.
        bool whole = absolute.Scale == 0;

        return language switch
        {
            "de" or "en" => whole && integerPart == 1 ? PluralCategory.One : PluralCategory.Other,
            "uk" => Ukrainian(integerPart, whole),
            _ => throw new NotSupportedException(
                $"No CLDR plural rule for '{language}'. Add one before shipping the language."),
        };
    }

    private static PluralCategory Ukrainian(long integerPart, bool whole)
    {
        if (!whole)
        {
            return PluralCategory.Other;
        }

        long lastDigit = integerPart % 10;
        long lastTwoDigits = integerPart % 100;

        if (lastDigit == 1 && lastTwoDigits != 11)
        {
            return PluralCategory.One;
        }

        return lastDigit is >= 2 and <= 4 && lastTwoDigits is < 12 or > 14
            ? PluralCategory.Few
            : PluralCategory.Many;
    }
}
