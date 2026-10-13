namespace Geteilt.Domain.Tests;

/// <summary>
/// The odd cent has to land on the same member every time, whatever order the members arrive in.
/// The rule is fixed: lowest member id, compared as text.
/// </summary>
public sealed class MoneyAllocateTieBreakTests
{
    private static readonly Guid Lowest = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Highest = new("ffffffff-ffff-ffff-ffff-ffffffffffff");

    [Fact]
    public void Allocate_WhenRemainderIsOneCent_GivesItToTheLowestMemberIdNotTheFirstArgument()
    {
        IReadOnlyList<decimal> shares = Money.Allocate(0.01m, [1m, 1m], [Highest, Lowest]);

        shares.Should().Equal(0.00m, 0.01m);
    }

    [Fact]
    public void Allocate_WhenTieBreaking_UsesOrdinalTextOrderOfTheIds()
    {
        Guid[] memberIds =
        [
            new("80000000-0000-0000-0000-000000000000"),
            new("00000001-0000-0000-0000-000000000000"),
            new("0fffffff-ffff-ffff-ffff-ffffffffffff"),
        ];

        Guid lowestAsText = memberIds.OrderBy(id => id.ToString(), StringComparer.Ordinal).First();
        lowestAsText.Should().Be(memberIds[1]);

        IReadOnlyList<decimal> shares = Money.Allocate(0.01m, [1m, 1m, 1m], memberIds);

        shares.Should().Equal(0.00m, 0.01m, 0.00m);
    }

    [Fact]
    public void Allocate_CalledTwiceWithTheSameInput_ReturnsTheSameShares()
    {
        IReadOnlyList<Guid> memberIds = MemberIds.Ascending(3);

        IReadOnlyList<decimal> first = Money.Allocate(10.00m, MemberIds.EqualWeights(3), memberIds);
        IReadOnlyList<decimal> second = Money.Allocate(10.00m, MemberIds.EqualWeights(3), memberIds);

        second.Should().Equal(first);
    }

    [Fact]
    public void Allocate_WhenInputOrderIsReversed_GivesEachMemberTheSameShare()
    {
        IReadOnlyList<Guid> memberIds = MemberIds.Ascending(3);
        IReadOnlyList<decimal> weights = [3m, 2m, 1m];

        IReadOnlyList<decimal> shares = Money.Allocate(10.00m, weights, memberIds);
        IReadOnlyList<decimal> reversedShares = Money.Allocate(
            10.00m,
            [.. weights.Reverse()],
            [.. memberIds.Reverse()]);

        reversedShares.Should().Equal([.. shares.Reverse()]);
    }
}
