using System.Collections.Frozen;

namespace Geteilt.Domain;

/// <summary>
/// ISO 4217 currency codes the application accepts. The list is baked in and never
/// fetched over the network.
/// </summary>
public static class Currency
{
    public const string Euro = "EUR";

    public const int CodeLength = 3;

    /// <summary>
    /// Deliberately not the full ISO 4217 register: this is the euro plus every currency the ECB
    /// publishes a daily reference rate for. A currency we cannot get a rate for is a currency we
    /// cannot convert, and an expense we cannot convert has no place in a group balance.
    /// </summary>
    private static readonly FrozenSet<string> Supported = new[]
    {
        Euro,
        "AUD", "BGN", "BRL", "CAD", "CHF", "CNY", "CZK", "DKK", "GBP", "HKD",
        "HUF", "IDR", "ILS", "INR", "ISK", "JPY", "KRW", "MXN", "MYR", "NOK",
        "NZD", "PHP", "PLN", "RON", "SEK", "SGD", "THB", "TRY", "USD", "ZAR",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Lets the lookup take a span, so invalid input never allocates a string.</summary>
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedLookup =
        Supported.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>Never throws: this runs on user input.</summary>
    public static bool IsValid(string? code) => TryNormalise(code, out _);

    /// <summary>Upper-cased code, for storing and comparing.</summary>
    /// <exception cref="ArgumentException">The code is not one of the supported currencies.</exception>
    public static string Normalise(string? code) =>
        TryNormalise(code, out string? normalised)
            ? normalised
            : throw new ArgumentException($"'{code}' is not a supported ISO 4217 currency code.", nameof(code));

    private static bool TryNormalise(string? code, out string normalised)
    {
        normalised = string.Empty;

        if (code is null)
        {
            return false;
        }

        ReadOnlySpan<char> trimmed = code.AsSpan().Trim();

        if (trimmed.Length != CodeLength)
        {
            return false;
        }

        Span<char> upper = stackalloc char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
        {
            if (!char.IsAsciiLetter(trimmed[i]))
            {
                return false;
            }

            upper[i] = char.ToUpperInvariant(trimmed[i]);
        }

        if (!SupportedLookup.TryGetValue(upper, out string? match))
        {
            return false;
        }

        normalised = match;
        return true;
    }
}
