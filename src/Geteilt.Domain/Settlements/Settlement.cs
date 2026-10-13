
namespace Geteilt.Domain.Settlements;

/// <summary>
/// A transfer that actually happened. The settlement plan is only a suggestion; this is the fact
/// that changes balances.
/// </summary>
public sealed class Settlement
{
    public const int MaxNoteLength = 200;

    private Settlement()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public Guid FromMemberId { get; private set; }

    public Guid ToMemberId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal AmountInBaseCurrency { get; private set; }

    public DateOnly Date { get; private set; }

    public SettlementMethod Method { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public static Settlement Create(
        Guid id,
        Guid groupId,
        Guid fromMemberId,
        Guid toMemberId,
        decimal amount,
        string currency,
        DateOnly date,
        DateTimeOffset createdAt,
        SettlementMethod method = SettlementMethod.BankTransfer,
        string? note = null)
    {
        if (amount <= 0m)
        {
            throw SettlementException.ForAmount(amount);
        }

        if (fromMemberId == toMemberId)
        {
            throw SettlementException.ForSelfTransfer(fromMemberId);
        }

        if (note is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(note.Length, MaxNoteLength);
        }

        return new Settlement
        {
            Id = id,
            GroupId = groupId,
            FromMemberId = fromMemberId,
            ToMemberId = toMemberId,
            Amount = amount,
            Currency = Domain.Currency.Normalise(currency),
            // An expense only uses the group base currency, so no conversion arises.
            AmountInBaseCurrency = amount,
            Date = date,
            Method = method,
            Note = note,
            CreatedAt = createdAt,
        };
    }

    public void Delete(DateTimeOffset deletedAt) => DeletedAt ??= deletedAt;
}
