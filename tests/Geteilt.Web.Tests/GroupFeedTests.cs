using Geteilt.Contracts.Expenses;
using Geteilt.Contracts.Groups;
using Geteilt.Web.Groups;

namespace Geteilt.Web.Tests;

/// <summary>A group with the part of its expense feed loaded so far.</summary>
public sealed class GroupFeedTests
{
    private static readonly GroupDto Trip = new(Guid.NewGuid(), "Trip", "EUR", DateTimeOffset.UnixEpoch, null, 2, 0m);

    [Fact]
    public void Append_AddsThePageAndTakesItsCursor()
    {
        GroupFeed feed = new(Trip, [Expense("Pizza")], "cursor-1");

        GroupFeed next = feed.Append(new ExpensePage([Expense("Wine")], "cursor-2"));

        next.Expenses.Select(expense => expense.Description).Should().Equal("Pizza", "Wine");
        next.NextCursor.Should().Be("cursor-2");
        feed.Expenses.Should().ContainSingle("the feed already on screen is not changed");
    }

    [Fact]
    public void TheLastPage_LeavesNothingMoreToLoad()
    {
        GroupFeed feed = new GroupFeed(Trip, [], "cursor-1").Append(new ExpensePage([], null));

        feed.HasMore.Should().BeFalse();
    }

    private static ExpenseDto Expense(string description) => new(
        Guid.NewGuid(), description, Guid.NewGuid(), "Anna", 10m, "EUR", 10m, 1m,
        new DateOnly(2026, 10, 9), SplitMethod.Equal, [], DateTimeOffset.UnixEpoch, null);
}
