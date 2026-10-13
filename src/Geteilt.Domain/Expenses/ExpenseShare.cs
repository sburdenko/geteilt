namespace Geteilt.Domain.Expenses;

/// <summary>
/// What one member owes for one expense. Stored, never recomputed on read: ten euro across three
/// people is 3.34 / 3.33 / 3.33, and which member carries the extra cent must not change because
/// somebody joined the group yesterday or the sort order changed.
/// </summary>
public sealed class ExpenseShare
{
    public ExpenseShare(
        Guid expenseId,
        Guid memberId,
        decimal shareAmount,
        decimal shareAmountInBaseCurrency,
        decimal? inputWeight = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shareAmount);
        ArgumentOutOfRangeException.ThrowIfNegative(shareAmountInBaseCurrency);

        ExpenseId = expenseId;
        MemberId = memberId;
        ShareAmount = shareAmount;
        ShareAmountInBaseCurrency = shareAmountInBaseCurrency;
        InputWeight = inputWeight;
    }

    /// <summary>Materialisation constructor for EF Core.</summary>
    private ExpenseShare()
    {
    }

    public Guid ExpenseId { get; private set; }

    public Guid MemberId { get; private set; }

    public decimal ShareAmount { get; private set; }

    public decimal ShareAmountInBaseCurrency { get; private set; }

    /// <summary>
    /// What the user originally typed for Shares or Percentages. Kept so reopening the expense for
    /// editing shows "2 : 1 : 1" again rather than the amounts it turned into.
    /// </summary>
    public decimal? InputWeight { get; private set; }
}
