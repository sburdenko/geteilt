namespace Geteilt.Domain;

/// <summary>
/// The single place where money is rounded. Business code never rounds on its own.
/// </summary>
public static class Money
{
    /// <summary>Largest amount the <c>numeric(18,2)</c> money columns can hold.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    private const decimal CentsPerUnit = 100m;

    /// <summary>
    /// Splits <paramref name="total"/> over the members proportionally to <paramref name="weights"/>,
    /// so that the shares add back up to <paramref name="total"/> exactly.
    /// </summary>
    /// <param name="total">Amount to split. Must be positive and a whole number of cents.</param>
    /// <param name="weights">
    /// Relative weight per member — an equal split, shares (2:1:1) and percentages all map onto this.
    /// Weights need not add up to anything in particular; only their ratio matters. Zero means "pays nothing".
    /// </param>
    /// <param name="memberIds">Member behind each weight, positionally aligned with <paramref name="weights"/>.</param>
    /// <returns>Share per member, aligned with the input order.</returns>
    public static IReadOnlyList<decimal> Allocate(
        decimal total,
        IReadOnlyList<decimal> weights,
        IReadOnlyList<Guid> memberIds)
    {
        Validate(total, weights, memberIds);

        // The whole calculation runs in whole cents. That makes "the leftover is a whole number of cents"
        // true by type instead of by hope, which is the property the largest-remainder step depends on.
        long totalCents = (long)(total * CentsPerUnit);
        decimal weightSum = weights.Sum();

        long[] cents = new long[weights.Count];
        decimal[] fractions = new decimal[weights.Count];
        long allocatedCents = 0;

        for (int i = 0; i < weights.Count; i++)
        {
            decimal exactCents = totalCents * weights[i] / weightSum;
            cents[i] = (long)decimal.Floor(exactCents);
            fractions[i] = exactCents - cents[i];
            allocatedCents += cents[i];
        }

        int remainderCents = (int)(totalCents - allocatedCents);
        foreach (int i in RemainderOrder(fractions, memberIds).Take(remainderCents))
        {
            cents[i]++;
        }

        return [.. cents.Select(memberCents => memberCents / CentsPerUnit)];
    }

    /// <summary>Who gets the leftover cents first: largest fractional part, then lowest member id.</summary>
    private static IEnumerable<int> RemainderOrder(decimal[] fractions, IReadOnlyList<Guid> memberIds)
    {
        // The tie-break is the *textual* order of the id. Guid.CompareTo would order by
        // the first field read as a signed int, a different order — and switching between the two silently
        // moves cents between people.
        string[] ids = [.. memberIds.Select(id => id.ToString())];

        return Enumerable.Range(0, fractions.Length)
            .OrderByDescending(i => fractions[i])
            .ThenBy(i => ids[i], StringComparer.Ordinal);
    }

    private static void Validate(decimal total, IReadOnlyList<decimal> weights, IReadOnlyList<Guid> memberIds)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(memberIds);

        if (weights.Count == 0)
        {
            throw new ArgumentException("At least one weight is required.", nameof(weights));
        }

        if (weights.Count != memberIds.Count)
        {
            throw new ArgumentException(
                $"Expected {weights.Count} member ids to go with the weights, got {memberIds.Count}.",
                nameof(memberIds));
        }

        if (memberIds.Distinct().Count() != memberIds.Count)
        {
            throw new ArgumentException(
                "Member ids must be unique, otherwise the tie-break has no single answer.",
                nameof(memberIds));
        }

        if (total <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(total),
                total,
                "Total must be positive: a refund is a settlement in the opposite direction, not a negative expense.");
        }

        if (total > MaxAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(total), total, $"Total must not exceed {MaxAmount}.");
        }

        if (total * CentsPerUnit != decimal.Truncate(total * CentsPerUnit))
        {
            throw new ArgumentOutOfRangeException(nameof(total), total, "Total must be a whole number of cents.");
        }

        foreach (decimal weight in weights)
        {
            if (weight < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(weights), weight, "Weights must not be negative.");
            }
        }

        if (weights.Sum() <= 0m)
        {
            throw new ArgumentException("At least one weight must be positive.", nameof(weights));
        }
    }
}
