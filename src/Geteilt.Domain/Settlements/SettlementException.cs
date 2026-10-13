using System.Globalization;

namespace Geteilt.Domain.Settlements;

/// <summary>A settlement would break one of its own invariants.</summary>
public sealed class SettlementException : DomainException
{
    public static readonly DomainError AmountMustBePositive = new(
        "amount_must_be_positive", DomainErrorKind.Validation, "A settlement amount must be positive");

    // There is no dedicated code for this, and inventing one for a case the UI cannot even
    // produce would inflate the contract; the generic validation code is enough.
    public static readonly DomainError SelfTransfer = new(
        "validation_failed", DomainErrorKind.Validation, "A settlement cannot go from a member to themselves");

    private SettlementException(DomainError error, string message)
        : base(error, message)
    {
    }

    public static SettlementException ForAmount(decimal amount) => new(
        AmountMustBePositive,
        string.Create(CultureInfo.InvariantCulture, $"Amount must be greater than zero, got {amount}."));

    public static SettlementException ForSelfTransfer(Guid memberId) => new(
        SelfTransfer,
        $"Member {memberId} cannot settle with themselves.");
}
