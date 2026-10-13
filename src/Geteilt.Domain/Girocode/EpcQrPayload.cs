using System.Globalization;
using System.Text;

namespace Geteilt.Domain.Girocode;

/// <summary>
/// Assembles the EPC069-12 v3.0 payload that goes inside a Girocode.
/// String assembly only — drawing the QR image is a separate concern, so this stays testable
/// byte for byte against the reference payloads.
/// </summary>
public static class EpcQrPayload
{
    public const int MaxBeneficiaryNameLength = 70;
    public const int MaxRemittanceBytes = 140;
    public const int MaxPayloadBytes = 331;
    public const decimal MinAmount = 0.01m;
    public const decimal MaxAmount = 999_999_999.99m;

    private const string ApplicationPrefix = "Geteilt: ";

    public static string Build(GirocodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Currency, Currency.Euro, StringComparison.OrdinalIgnoreCase))
        {
            throw EpcQrException.ForCurrency(request.Currency);
        }

        if (string.IsNullOrWhiteSpace(request.Iban))
        {
            throw EpcQrException.ForMissingIban();
        }

        if (!Iban.TryNormalise(request.Iban, out string iban))
        {
            throw EpcQrException.ForInvalidIban();
        }

        if (request.Amount < MinAmount || request.Amount > MaxAmount)
        {
            throw EpcQrException.ForAmount(request.Amount);
        }

        string[] lines =
        [
            "BCD",
            "002",
            "1",
            "SCT",
            string.Empty,
            TruncateToCharacters(request.BeneficiaryName, MaxBeneficiaryNameLength),
            iban,
            // Invariant culture is not a detail: under de-DE the default formatting produces
            // "EUR12,30", and a comma makes the payload unreadable to the bank.
            string.Create(CultureInfo.InvariantCulture, $"EUR{request.Amount:F2}"),
            string.Empty,
            string.Empty,
            TruncateToBytes(request.Remittance, MaxRemittanceBytes),
        ];

        string payload = string.Join('\n', lines[..LengthWithoutTrailingBlanks(lines)]);

        int byteCount = Encoding.UTF8.GetByteCount(payload);
        if (byteCount > MaxPayloadBytes)
        {
            throw EpcQrException.ForPayloadLength(byteCount);
        }

        return payload;
    }

    /// <summary>
    /// The payment reference is generated, never typed by a user. The prefix survives
    /// truncation for free because it comes first: a long group name loses its tail, not the app name.
    /// </summary>
    public static string RemittanceFor(string groupName) =>
        TruncateToBytes(ApplicationPrefix + groupName, MaxRemittanceBytes);

    private static int LengthWithoutTrailingBlanks(string[] lines)
    {
        int length = lines.Length;

        while (length > 0 && lines[length - 1].Length == 0)
        {
            length--;
        }

        return length;
    }

    private static string TruncateToCharacters(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        // Cutting between the halves of a surrogate pair would leave a lone half, which renders
        // as a replacement character.
        int cut = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;

        return value[..cut];
    }

    /// <summary>
    /// Trims to a byte budget on a character boundary. The limit in the standard counts bytes, and
    /// 'ä' costs two of them — cutting at a byte offset would produce a broken string the bank
    /// cannot decode.
    /// </summary>
    private static string TruncateToBytes(string value, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
        {
            return value;
        }

        int usedBytes = 0;
        int takenChars = 0;

        foreach (Rune rune in value.EnumerateRunes())
        {
            if (usedBytes + rune.Utf8SequenceLength > maxBytes)
            {
                break;
            }

            usedBytes += rune.Utf8SequenceLength;
            takenChars += rune.Utf16SequenceLength;
        }

        return value[..takenChars];
    }
}
