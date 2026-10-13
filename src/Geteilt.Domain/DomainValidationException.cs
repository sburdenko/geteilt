namespace Geteilt.Domain;

/// <summary>
/// A value the caller supplied is not one the domain accepts. Named after the failure rather than
/// after an entity, because the same two checks are shared: an IBAN belongs to a
/// <see cref="Entities.User"/> and to a <see cref="Entities.Member"/>, a currency code to a
/// <see cref="Entities.Group"/> and to an <see cref="Entities.Expense"/>.
/// </summary>
/// <remarks>
/// Distinct from the <see cref="ArgumentException"/> family that <see cref="Money"/> and
/// <see cref="Currency"/> raise. Those are stateless helpers whose preconditions are the calling
/// code's contract; this one is reached by data a person typed, and owes them 422 with a reason
/// rather than a 500.
/// </remarks>
public sealed class DomainValidationException : DomainException
{
    public static readonly DomainError InvalidIban =
        new("invalid_iban", DomainErrorKind.Validation, "The IBAN is not valid");

    public static readonly DomainError InvalidCurrency =
        new("invalid_currency", DomainErrorKind.Validation, "The currency code is not supported");

    private DomainValidationException(DomainError error, string message)
        : base(error, message)
    {
    }

    public static DomainValidationException ForIban(string iban) => new(
        InvalidIban,
        $"'{iban}' does not pass the IBAN check digits or has the wrong length for its country.");

    public static DomainValidationException ForCurrency(string currency) => new(
        InvalidCurrency,
        $"'{currency}' is not a currency this application can convert.");
}
