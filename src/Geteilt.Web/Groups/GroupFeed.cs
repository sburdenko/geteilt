using Geteilt.Contracts.Expenses;
using Geteilt.Contracts.Groups;

namespace Geteilt.Web.Groups;

/// <summary>A group with as much of its expense feed as has been loaded so far.</summary>
/// <param name="NextCursor">Handed back exactly as it arrived; null once the last page is in.</param>
public sealed record GroupFeed(GroupDto Group, IReadOnlyList<ExpenseDto> Expenses, string? NextCursor)
{
    public bool HasMore => NextCursor is not null;

    public GroupFeed Append(ExpensePage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return this with { Expenses = [.. Expenses, .. page.Items], NextCursor = page.NextCursor };
    }
}
