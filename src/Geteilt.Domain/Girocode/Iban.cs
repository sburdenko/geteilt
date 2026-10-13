namespace Geteilt.Domain.Girocode;

/// <summary>
/// IBAN validation per ISO 13616. Written by hand rather than taken from a package:
/// it is a mod-97 checksum and a length table, and a dependency would cost more than it saves.
/// </summary>
public static class Iban
{
    private const int MinLength = 15;
    private const int MaxLength = 34;
    private const int CountryCodeLength = 2;
    private const int CheckDigitsLength = 2;

    /// <summary>
    /// Never throws — anything that is not a valid IBAN is simply <see langword="false"/>, because
    /// this is called on user input.
    /// </summary>
    public static bool IsValid(string? iban) => TryNormalise(iban, out _);

    /// <summary>
    /// Validates and returns the canonical form: no spaces, upper case. That is the shape a
    /// Girocode payload needs.
    /// </summary>
    public static bool TryNormalise(string? iban, out string normalised)
    {
        normalised = string.Empty;

        if (iban is null)
        {
            return false;
        }

        Span<char> buffer = stackalloc char[MaxLength];

        if (!TryPack(iban, buffer, out int length) || length < MinLength)
        {
            return false;
        }

        ReadOnlySpan<char> account = buffer[..length];

        if (LengthFor(account[..CountryCodeLength]) != length)
        {
            return false;
        }

        for (int i = CountryCodeLength; i < CountryCodeLength + CheckDigitsLength; i++)
        {
            if (!char.IsAsciiDigit(account[i]))
            {
                return false;
            }
        }

        if (!TryComputeMod97(account, out int remainder) || remainder != 1)
        {
            return false;
        }

        normalised = new string(account);
        return true;
    }

    private static bool TryPack(string input, Span<char> destination, out int length)
    {
        length = 0;

        foreach (char symbol in input)
        {
            if (symbol == ' ')
            {
                continue;
            }

            if (length == destination.Length)
            {
                return false;
            }

            destination[length++] = char.ToUpperInvariant(symbol);
        }

        return true;
    }

    /// <summary>
    /// Accumulates the remainder character by character, so the 30-plus digit number the standard
    /// describes never has to be built — no BigInteger, no allocation.
    /// </summary>
    private static bool TryComputeMod97(ReadOnlySpan<char> account, out int remainder)
    {
        remainder = 0;

        for (int i = 0; i < account.Length; i++)
        {
            // Reading from an offset of four is the "move the first four characters to the end" step,
            // done without copying the string.
            char symbol = account[(i + CountryCodeLength + CheckDigitsLength) % account.Length];

            if (char.IsAsciiDigit(symbol))
            {
                remainder = ((remainder * 10) + (symbol - '0')) % 97;
            }
            else if (char.IsAsciiLetterUpper(symbol))
            {
                // A letter expands to two digits, so the accumulator advances by two places.
                remainder = ((remainder * 100) + (symbol - 'A' + 10)) % 97;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Length per country. An unknown country code is rejected rather than length-checked loosely:
    /// a typo in the country code is far more likely than an IBAN from outside SEPA.
    /// </summary>
    private static int LengthFor(ReadOnlySpan<char> countryCode) => countryCode switch
    {
        "AD" => 24,
        "AT" => 20,
        "BE" => 16,
        "BG" => 22,
        "CH" => 21,
        "CY" => 28,
        "CZ" => 24,
        "DE" => 22,
        "DK" => 18,
        "EE" => 20,
        "ES" => 24,
        "FI" => 18,
        "FR" => 27,
        "GB" => 22,
        "GR" => 27,
        "HR" => 21,
        "HU" => 28,
        "IE" => 22,
        "IS" => 26,
        "IT" => 27,
        "LI" => 21,
        "LT" => 20,
        "LU" => 20,
        "LV" => 21,
        "MC" => 27,
        "MT" => 31,
        "NL" => 18,
        "NO" => 15,
        "PL" => 28,
        "PT" => 25,
        "RO" => 24,
        "SE" => 24,
        "SI" => 19,
        "SK" => 24,
        "SM" => 27,
        "VA" => 22,
        _ => 0,
    };
}
