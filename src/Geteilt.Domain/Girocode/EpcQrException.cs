using System.Globalization;

namespace Geteilt.Domain.Girocode;

/// <summary>A Girocode cannot be produced from the given data.</summary>
public sealed class EpcQrException : DomainException
{
    // Every one of these is a validation failure: the data handed in cannot produce a Girocode,
    // and no change in system state would make it acceptable.
    public static readonly DomainError OnlyEuro =
        new("epc_only_eur", DomainErrorKind.Validation, "A Girocode can only carry euro");

    public static readonly DomainError AmountOutOfRange =
        new("epc_amount_out_of_range", DomainErrorKind.Validation, "Amount is outside the range a Girocode allows");

    public static readonly DomainError PayloadTooLong =
        new("epc_payload_too_long", DomainErrorKind.Validation, "Girocode payload exceeds 331 bytes");

    public static readonly DomainError NoIban =
        new("epc_no_iban", DomainErrorKind.Validation, "The beneficiary has no IBAN");

    public static readonly DomainError InvalidIban =
        new("invalid_iban", DomainErrorKind.Validation, "The IBAN is not valid");

    private EpcQrException(DomainError error, string message)
        : base(error, message)
    {
    }

    public static EpcQrException ForCurrency(string currency) => new(
        OnlyEuro,
        $"SEPA credit transfer knows no currency but EUR. Got '{currency}'.");

    public static EpcQrException ForAmount(decimal amount) => new(
        AmountOutOfRange,
        string.Create(CultureInfo.InvariantCulture, $"Amount {amount} is outside the range 0.01..999999999.99."));

    public static EpcQrException ForPayloadLength(int byteCount) => new(
        PayloadTooLong,
        string.Create(CultureInfo.InvariantCulture, $"Payload is {byteCount} bytes, the limit is 331."));

    public static EpcQrException ForMissingIban() => new(
        NoIban,
        "The beneficiary has no IBAN, so there is nothing to transfer to.");

    public static EpcQrException ForInvalidIban() => new(
        InvalidIban,
        "The beneficiary IBAN does not pass the mod-97 check.");
}
