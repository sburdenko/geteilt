namespace Geteilt.Contracts.Expenses;

/// <param name="Amount">Used by <see cref="SplitMethod.ExactAmounts"/>, ignored otherwise.</param>
/// <param name="Weight">
/// Used by <see cref="SplitMethod.Shares"/> and <see cref="SplitMethod.Percentages"/>. For an even
/// split neither is needed — naming the member is enough.
/// </param>
public sealed record ShareInputDto(Guid MemberId, decimal? Amount, decimal? Weight);
