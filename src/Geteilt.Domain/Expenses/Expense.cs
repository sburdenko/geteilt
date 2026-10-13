
namespace Geteilt.Domain.Expenses;

/// <summary>
/// One thing somebody paid for, together with how it splits. The shares are part of the expense
/// and never exist without it, which is what lets the "shares add up to the amount" invariant be
/// enforced in one place instead of hoped for across a service layer.
/// </summary>
public sealed class Expense
{
    public const int MaxDescriptionLength = 200;

    /// <summary>Timezones: a phone an hour ahead must not have its expense rejected.</summary>
    private const int AllowedDaysAhead = 1;

    private readonly List<ExpenseShare> _shares = [];

    private Expense(NewExpense draft, decimal amountInBaseCurrency)
    {
        Id = draft.Id;
        GroupId = draft.GroupId;
        Description = draft.Description;
        PaidByMemberId = draft.PaidByMemberId;
        Amount = draft.Amount;
        Currency = Domain.Currency.Normalise(draft.Currency);
        ExchangeRate = draft.ExchangeRate;
        AmountInBaseCurrency = amountInBaseCurrency;
        Date = draft.Date;
        SplitMethod = draft.SplitMethod;
        CreatedByMemberId = draft.CreatedByMemberId;
        CreatedAt = draft.CreatedAt;
    }

    /// <summary>Materialisation constructor for EF Core.</summary>
    private Expense()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public Guid PaidByMemberId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal ExchangeRate { get; private set; } = 1.000000m;

    /// <summary>
    /// Denormalised on purpose. The rate on the day of the expense is a historical fact; recompute
    /// it tomorrow and balances shift, so a user watches a debt change by itself. That is the worst
    /// bug an application about money can have.
    /// </summary>
    public decimal AmountInBaseCurrency { get; private set; }

    public DateOnly Date { get; private set; }

    public SplitMethod SplitMethod { get; private set; }

    public Guid CreatedByMemberId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public IReadOnlyCollection<ExpenseShare> Shares => _shares;

    public static Expense Create(
        NewExpense draft,
        IReadOnlyList<ExpenseShare> shares,
        IReadOnlyCollection<Guid> groupMemberIds,
        string groupBaseCurrency)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(shares);
        ArgumentNullException.ThrowIfNull(groupMemberIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Description);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(draft.Description.Length, MaxDescriptionLength);

        if (shares.Count == 0)
        {
            throw new ArgumentException("An expense must be split over at least one member.", nameof(shares));
        }

        if (draft.Amount <= 0m)
        {
            throw ExpenseException.ForAmount(draft.Amount);
        }

        string currency = Domain.Currency.Normalise(draft.Currency);
        if (!string.Equals(currency, groupBaseCurrency, StringComparison.Ordinal))
        {
            throw ExpenseException.ForCurrency(currency, groupBaseCurrency);
        }

        DateOnly latestAllowed = DateOnly.FromDateTime(draft.CreatedAt.UtcDateTime).AddDays(AllowedDaysAhead);
        if (draft.Date > latestAllowed)
        {
            throw ExpenseException.ForFutureDate(draft.Date);
        }

        if (!groupMemberIds.Contains(draft.PaidByMemberId))
        {
            throw ExpenseException.ForMemberOutsideGroup(draft.PaidByMemberId);
        }

        foreach (ExpenseShare share in shares)
        {
            if (!groupMemberIds.Contains(share.MemberId))
            {
                throw ExpenseException.ForMemberOutsideGroup(share.MemberId);
            }
        }

        if (shares.Select(share => share.MemberId).Distinct().Count() != shares.Count)
        {
            throw new ArgumentException("A member can hold at most one share of an expense.", nameof(shares));
        }

        decimal sharesTotal = shares.Sum(share => share.ShareAmount);
        if (sharesTotal != draft.Amount)
        {
            throw ExpenseException.ForSharesSum(sharesTotal, draft.Amount);
        }

        decimal amountInBaseCurrency = ToBaseCurrency(draft.Amount, draft.ExchangeRate);

        decimal baseSharesTotal = shares.Sum(share => share.ShareAmountInBaseCurrency);
        if (baseSharesTotal != amountInBaseCurrency)
        {
            throw ExpenseException.ForSharesSum(baseSharesTotal, amountInBaseCurrency);
        }

        Expense expense = new(draft, amountInBaseCurrency);
        expense._shares.AddRange(shares);

        return expense;
    }

    /// <summary>
    /// Replaces everything the caller may change, shares included. The whole set goes at once
    /// rather than share by share: the invariant is about the set as a whole, and a half-applied
    /// edit would leave the sum wrong between two saves.
    /// </summary>
    public void Update(
        NewExpense draft,
        IReadOnlyList<ExpenseShare> shares,
        IReadOnlyCollection<Guid> groupMemberIds,
        string groupBaseCurrency,
        DateTimeOffset updatedAt)
    {
        // Validated by building a replacement through exactly the same path a new expense takes,
        // so an edit can never reach a state a creation would have refused.
        Expense replacement = Create(draft with { Id = Id }, shares, groupMemberIds, groupBaseCurrency);

        Description = replacement.Description;
        PaidByMemberId = replacement.PaidByMemberId;
        Amount = replacement.Amount;
        Currency = replacement.Currency;
        ExchangeRate = replacement.ExchangeRate;
        AmountInBaseCurrency = replacement.AmountInBaseCurrency;
        Date = replacement.Date;
        SplitMethod = replacement.SplitMethod;
        UpdatedAt = updatedAt;

        _shares.Clear();
        _shares.AddRange(replacement.Shares);
    }

    /// <summary>
    /// Soft delete: the row stays so history stays consistent. Calling it twice keeps the
    /// first moment — a second delete is not a new event, and DELETE is meant to be repeatable.
    /// </summary>
    public void Delete(DateTimeOffset deletedAt) => DeletedAt ??= deletedAt;

    /// <summary>
    /// Every expense is in the group's own currency, so the rate is 1 and this never has to round.
    /// It deliberately refuses to invent a rounding rule for a conversion: silently picking one is
    /// exactly the kind of decision that turns up months later as money that does not add up.
    /// </summary>
    private static decimal ToBaseCurrency(decimal amount, decimal exchangeRate)
    {
        decimal converted = amount * exchangeRate;

        if (converted != decimal.Truncate(converted * 100m) / 100m)
        {
            throw new NotSupportedException(
                "Converting at a rate that does not land on whole cents needs a rounding rule, "
                + "and none is defined.");
        }

        return converted;
    }
}
