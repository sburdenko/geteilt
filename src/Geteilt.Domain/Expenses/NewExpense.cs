namespace Geteilt.Domain.Expenses;

/// <summary>
/// The facts needed to create an <see cref="Expense"/>. A record rather than a dozen positional
/// arguments: three of them are <see cref="Guid"/> and two are <see cref="decimal"/>, and nothing
/// but the parameter name would catch two of them being swapped.
/// </summary>
public sealed record NewExpense
{
    public required Guid Id { get; init; }

    public required Guid GroupId { get; init; }

    public required string Description { get; init; }

    public required Guid PaidByMemberId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required DateOnly Date { get; init; }

    public required SplitMethod SplitMethod { get; init; }

    public required Guid CreatedByMemberId { get; init; }

    /// <summary>Supplied by the caller from <see cref="TimeProvider"/>; also fixes what "today" means.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Rate to the group base currency on the day of the expense. Always 1: an expense is in the group's own currency.</summary>
    public decimal ExchangeRate { get; init; } = 1.000000m;
}
