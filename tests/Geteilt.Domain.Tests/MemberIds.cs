namespace Geteilt.Domain.Tests;

internal static class MemberIds
{
    // Money.Allocate breaks ties on the string form of the id, so ids that are ascending as text
    // are also ascending for the tie-break. The reference tables assume exactly that.
    internal static IReadOnlyList<Guid> Ascending(int count) =>
        [.. Enumerable.Range(1, count).Select(i => new Guid($"00000000-0000-0000-0000-{i:D12}"))];

    internal static IReadOnlyList<decimal> EqualWeights(int count) => [.. Enumerable.Repeat(1m, count)];
}
