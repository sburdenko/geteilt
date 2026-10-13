using System.Globalization;

namespace Geteilt.Domain.Expenses;

/// <summary>An expense would break one of its own invariants.</summary>
public sealed class ExpenseException : DomainException
{
    public static readonly DomainError AmountMustBePositive = new(
        "amount_must_be_positive", DomainErrorKind.Validation, "An expense amount must be positive");

    public static readonly DomainError SharesSumMismatch = new(
        "shares_sum_mismatch", DomainErrorKind.Validation, "Shares do not add up to the expense amount");

    public static readonly DomainError PercentagesSumMismatch = new(
        "percentages_sum_mismatch", DomainErrorKind.Validation, "Percentages do not add up to 100.00");

    public static readonly DomainError MemberNotInGroup = new(
        "member_not_in_group", DomainErrorKind.Validation, "The member does not belong to this group");

    public static readonly DomainError DateInFuture = new(
        "date_in_future", DomainErrorKind.Validation, "An expense cannot be dated in the future");

    public static readonly DomainError CurrencyMismatch = new(
        "currency_mismatch", DomainErrorKind.Validation, "The expense currency differs from the group base currency");

    private ExpenseException(DomainError error, string message)
        : base(error, message)
    {
    }

    /// <summary>
    /// One member listed twice in one split. A validation failure rather than a programming error:
    /// a form that lets the same person be picked twice will send exactly this.
    /// </summary>
    public static readonly DomainError DuplicateMember = new(
        "validation_failed", DomainErrorKind.Validation, "A member appears twice in the split");

    public static ExpenseException ForDuplicateMember() => new(
        DuplicateMember,
        "A member can hold at most one share of an expense.");

    public static ExpenseException ForAmount(decimal amount) => new(
        AmountMustBePositive,
        string.Create(CultureInfo.InvariantCulture, $"Amount must be greater than zero, got {amount}. A refund is a settlement in the opposite direction."));

    public static ExpenseException ForSharesSum(decimal sharesTotal, decimal amount) => new(
        SharesSumMismatch,
        string.Create(CultureInfo.InvariantCulture, $"Shares add up to {sharesTotal} but the expense is {amount}."));

    public static ExpenseException ForPercentagesSum(decimal total) => new(
        PercentagesSumMismatch,
        string.Create(CultureInfo.InvariantCulture, $"Percentages add up to {total}, they must add up to 100.00."));

    public static ExpenseException ForMemberOutsideGroup(Guid memberId) => new(
        MemberNotInGroup,
        $"Member {memberId} does not belong to this group.");

    public static ExpenseException ForFutureDate(DateOnly date) => new(
        DateInFuture,
        $"Expense date {date:yyyy-MM-dd} lies more than a day ahead.");

    public static ExpenseException ForCurrency(string expenseCurrency, string baseCurrency) => new(
        CurrencyMismatch,
        $"Expense currency {expenseCurrency} differs from the group base currency {baseCurrency}; an expense is in the group's own currency.");
}
