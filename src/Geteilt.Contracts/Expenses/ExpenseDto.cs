namespace Geteilt.Contracts.Expenses;

/// <param name="ExchangeRate">
/// Rate to the group's base currency on the day of the expense. Always 1.000000: an expense is in
/// the group's own currency.
/// </param>
public sealed record ExpenseDto(
    Guid Id,
    string Description,
    Guid PaidByMemberId,
    string PaidByName,
    decimal Amount,
    string Currency,
    decimal AmountInBaseCurrency,
    decimal ExchangeRate,
    DateOnly Date,
    SplitMethod SplitMethod,
    IReadOnlyList<ExpenseShareDto> Shares,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
