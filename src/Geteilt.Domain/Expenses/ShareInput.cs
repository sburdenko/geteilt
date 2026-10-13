namespace Geteilt.Domain.Expenses;

/// <summary>
/// One line of what the user typed when splitting an expense. What <paramref name="Value"/> means
/// depends on the split method: a weight for Shares, a percent for Percentages, an amount for
/// ExactAmounts. An Equal split takes no value at all, and supplying one is rejected rather than
/// quietly ignored.
/// </summary>
public sealed record ShareInput(Guid MemberId, decimal? Value = null);
