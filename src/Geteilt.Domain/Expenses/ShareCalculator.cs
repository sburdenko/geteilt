
namespace Geteilt.Domain.Expenses;

/// <summary>
/// Turns "100 euro, split 2:1:1" into a share per member. Every method except
/// ExactAmounts goes through <see cref="Money.Allocate"/>, so the shares add up to the total by
/// construction rather than by a check afterwards.
/// </summary>
public static class ShareCalculator
{
    private const decimal FullPercentage = 100.00m;

    public static IReadOnlyList<ExpenseShare> Calculate(
        Guid expenseId,
        decimal amount,
        SplitMethod method,
        IReadOnlyList<ShareInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        // Checked here as well as in Expense.Create, because this runs first. Money.Allocate
        // rejects a non-positive total with an ArgumentException — right for a stateless helper
        // whose preconditions are the caller's contract, but it would reach the API as a
        // 500. The amount arrives from a form, so it owes the caller 422 amount_must_be_positive.
        if (amount <= 0m)
        {
            throw ExpenseException.ForAmount(amount);
        }

        if (inputs.Count == 0)
        {
            throw new ArgumentException("An expense must be split over at least one member.", nameof(inputs));
        }

        if (inputs.Select(input => input.MemberId).Distinct().Count() != inputs.Count)
        {
            throw ExpenseException.ForDuplicateMember();
        }

        return method switch
        {
            SplitMethod.Equal => Allocate(expenseId, amount, inputs, EqualWeights(inputs), keepInputWeight: false),
            SplitMethod.Shares => Allocate(expenseId, amount, inputs, PositiveValues(inputs), keepInputWeight: true),
            SplitMethod.Percentages => Allocate(expenseId, amount, inputs, Percentages(inputs), keepInputWeight: true),
            SplitMethod.ExactAmounts => ExactAmounts(expenseId, amount, inputs),
            _ => throw new NotSupportedException($"Split method {method} is not supported."),
        };
    }

    private static decimal[] EqualWeights(IReadOnlyList<ShareInput> inputs)
    {
        if (inputs.Any(input => input.Value is not null))
        {
            throw new ArgumentException(
                "An equal split takes no per-member value; supplying one is a mistake, not a hint.",
                nameof(inputs));
        }

        return [.. Enumerable.Repeat(1m, inputs.Count)];
    }

    private static decimal[] PositiveValues(IReadOnlyList<ShareInput> inputs)
    {
        decimal[] weights = ValuesOf(inputs);

        foreach (decimal weight in weights)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weight, nameof(inputs));
        }

        return weights;
    }

    private static decimal[] Percentages(IReadOnlyList<ShareInput> inputs)
    {
        decimal[] percentages = PositiveValues(inputs);
        decimal total = percentages.Sum();

        if (total != FullPercentage)
        {
            throw ExpenseException.ForPercentagesSum(total);
        }

        return percentages;
    }

    private static decimal[] ValuesOf(IReadOnlyList<ShareInput> inputs)
    {
        decimal[] values = new decimal[inputs.Count];

        for (int i = 0; i < inputs.Count; i++)
        {
            values[i] = inputs[i].Value
                        ?? throw new ArgumentException(
                            $"Member {inputs[i].MemberId} has no value, which this split method requires.",
                            nameof(inputs));
        }

        return values;
    }

    private static IReadOnlyList<ExpenseShare> Allocate(
        Guid expenseId,
        decimal amount,
        IReadOnlyList<ShareInput> inputs,
        decimal[] weights,
        bool keepInputWeight)
    {
        Guid[] memberIds = [.. inputs.Select(input => input.MemberId)];
        IReadOnlyList<decimal> shares = Money.Allocate(amount, weights, memberIds);

        return [.. shares.Select((share, i) => NewShare(
            expenseId,
            memberIds[i],
            share,
            keepInputWeight ? weights[i] : null))];
    }

    /// <summary>
    /// Taken exactly as typed: the user already decided the cents, and running them through the
    /// largest-remainder split would move money they deliberately placed.
    /// </summary>
    private static IReadOnlyList<ExpenseShare> ExactAmounts(
        Guid expenseId,
        decimal amount,
        IReadOnlyList<ShareInput> inputs)
    {
        decimal[] amounts = ValuesOf(inputs);

        foreach (decimal share in amounts)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(share, nameof(inputs));
        }

        decimal total = amounts.Sum();
        if (total != amount)
        {
            throw ExpenseException.ForSharesSum(total, amount);
        }

        return [.. inputs.Select((input, i) => NewShare(expenseId, input.MemberId, amounts[i], inputWeight: null))];
    }

    // An expense is always in the group base currency, so the two amounts are the same number.
    // Should conversion ever exist, the base amount stops being a copy.
    private static ExpenseShare NewShare(Guid expenseId, Guid memberId, decimal share, decimal? inputWeight) =>
        new(expenseId, memberId, share, share, inputWeight);
}
