namespace Geteilt.Domain.Tests;

/// <summary>Weighted splits — shares and percentages.</summary>
public sealed class MoneyAllocateWeightedTests
{
    public static TheoryData<decimal, decimal[], decimal[]> ReferenceTable() => new()
    {
        { 100.00m, [2m, 1m, 1m], [50.00m, 25.00m, 25.00m] },
        { 100.00m, [1m, 1m, 1m], [33.34m, 33.33m, 33.33m] },
        { 10.00m, [3m, 1m], [7.50m, 2.50m] },
        { 100.00m, [33.33m, 33.33m, 33.34m], [33.33m, 33.33m, 33.34m] },
        { 50.00m, [1m, 2m, 3m, 4m], [5.00m, 10.00m, 15.00m, 20.00m] },
    };

    [Theory]
    [MemberData(nameof(ReferenceTable))]
    public void Allocate_WithWeights_MatchesReferenceTable(decimal total, decimal[] weights, decimal[] expected)
    {
        IReadOnlyList<decimal> shares = Money.Allocate(total, weights, MemberIds.Ascending(weights.Length));

        shares.Should().Equal(expected);
    }

    [Fact]
    public void Allocate_WhenMemberHasZeroWeight_GivesThatMemberNothing()
    {
        IReadOnlyList<decimal> shares = Money.Allocate(10.00m, [1m, 0m, 1m], MemberIds.Ascending(3));

        shares.Should().Equal(5.00m, 0.00m, 5.00m);
    }
}
