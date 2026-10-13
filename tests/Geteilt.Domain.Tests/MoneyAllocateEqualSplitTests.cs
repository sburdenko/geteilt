namespace Geteilt.Domain.Tests;

/// <summary>Equal splits — a reference table of amounts, people and the expected cents.</summary>
public sealed class MoneyAllocateEqualSplitTests
{
    public static TheoryData<decimal, int, decimal[]> ReferenceTable() => new()
    {
        { 10.00m, 3, [3.34m, 3.33m, 3.33m] },
        { 100.00m, 3, [33.34m, 33.33m, 33.33m] },
        { 0.01m, 2, [0.01m, 0.00m] },
        { 0.02m, 3, [0.01m, 0.01m, 0.00m] },
        { 33.33m, 2, [16.67m, 16.66m] },
        { 7.00m, 6, [1.17m, 1.17m, 1.17m, 1.17m, 1.16m, 1.16m] },
        { 100.00m, 1, [100.00m] },
        { 99.99m, 3, [33.33m, 33.33m, 33.33m] },
    };

    [Theory]
    [MemberData(nameof(ReferenceTable))]
    public void Allocate_WhenSplitEqually_MatchesReferenceTable(decimal total, int memberCount, decimal[] expected)
    {
        IReadOnlyList<decimal> shares = Money.Allocate(
            total,
            MemberIds.EqualWeights(memberCount),
            MemberIds.Ascending(memberCount));

        shares.Should().Equal(expected);
    }
}
