namespace Geteilt.Contracts.Expenses;

/// <param name="NextCursor">
/// Opaque: hand it back untouched to get the following page. Null means this was the last one.
/// Clients must not build or parse it — its contents are ours to change.
/// </param>
public sealed record ExpensePage(IReadOnlyList<ExpenseDto> Items, string? NextCursor);
