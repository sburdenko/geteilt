using CsCheck;

namespace Geteilt.Domain.Tests;

/// <summary>
/// The table tests say what happens for the cases someone thought of,
/// these say what must hold for every input at all.
/// </summary>
public sealed class MoneyAllocatePropertyTests
{
    private const int Runs = 1000;

    /// <summary>Totals from 0.01 up to 10 000 000.00, always a whole number of cents.</summary>
    private static readonly Gen<decimal> Totals = Gen.Long[1, 1_000_000_000].Select(cents => cents / 100m);

    /// <summary>Between one and twelve non-negative weights, at least one of them positive.</summary>
    private static readonly Gen<decimal[]> Weights =
        Gen.Int[0, 1000].Array[1, 12]
            .Where(weights => weights.Any(weight => weight > 0))
            .Select(weights => Array.ConvertAll(weights, weight => (decimal)weight));

    [Fact]
    public void Allocate_ForAnyInput_SharesAddUpToTotal()
    {
        Gen.Select(Totals, Weights).Sample(
            (total, weights) =>
            {
                IReadOnlyList<decimal> shares = Money.Allocate(total, weights, MemberIds.Ascending(weights.Length));

                shares.Sum().Should().Be(total);
            },
            iter: Runs);
    }

    [Fact]
    public void Allocate_ForAnyInput_ProducesNonNegativeWholeCentShares()
    {
        Gen.Select(Totals, Weights).Sample(
            (total, weights) =>
            {
                IReadOnlyList<decimal> shares = Money.Allocate(total, weights, MemberIds.Ascending(weights.Length));

                shares.Should().OnlyContain(share => share >= 0m);
                shares.Should().OnlyContain(share => share * 100m == decimal.Truncate(share * 100m));
            },
            iter: Runs);
    }

    [Fact]
    public void Allocate_ForAnyInput_NeverMovesMoreThanOneCentAwayFromTheExactShare()
    {
        Gen.Select(Totals, Weights).Sample(
            (total, weights) =>
            {
                IReadOnlyList<decimal> shares = Money.Allocate(total, weights, MemberIds.Ascending(weights.Length));

                decimal weightSum = weights.Sum();
                for (int i = 0; i < weights.Length; i++)
                {
                    decimal exact = total * weights[i] / weightSum;
                    Math.Abs(shares[i] - exact).Should().BeLessThan(0.01m);
                }
            },
            iter: Runs);
    }

    [Fact]
    public void Allocate_ForAnyInput_IgnoresTheOrderMembersArriveIn()
    {
        Gen.Select(Totals, Weights, Gen.Int[0, int.MaxValue]).Sample(
            (total, weights, seed) =>
            {
                IReadOnlyList<Guid> memberIds = MemberIds.Ascending(weights.Length);
                int[] permutation = Permutation(weights.Length, seed);

                IReadOnlyList<decimal> shares = Money.Allocate(total, weights, memberIds);
                IReadOnlyList<decimal> permutedShares = Money.Allocate(
                    total,
                    [.. permutation.Select(i => weights[i])],
                    [.. permutation.Select(i => memberIds[i])]);

                for (int i = 0; i < permutation.Length; i++)
                {
                    permutedShares[i].Should().Be(shares[permutation[i]]);
                }
            },
            iter: Runs);
    }

    private static int[] Permutation(int length, int seed)
    {
        int[] indices = [.. Enumerable.Range(0, length)];
        Random random = new(seed);
        for (int i = length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices;
    }
}
