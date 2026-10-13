namespace Geteilt.Domain.Balances;

/// <summary>
/// Works out who should pay whom to clear a group. Greedy: the deepest debtor pays
/// the largest creditor, repeatedly.
/// </summary>
/// <remarks>
/// This is not the shortest possible plan. Finding that is NP-hard; greedy can
/// need three transfers where two would do. In a group of five nobody notices, and in exchange the
/// algorithm is O(N log N), instant, and simple enough to explain out loud.
/// </remarks>
public static class SettlementPlanner
{
    public static IReadOnlyList<SettlementTransfer> Plan(IReadOnlyCollection<MemberBalance> balances)
    {
        ArgumentNullException.ThrowIfNull(balances);

        decimal total = balances.Sum(balance => balance.Balance);
        if (total != 0m)
        {
            // Not a defensive check but a real signal: balances that do not cancel mean cents were
            // lost upstream, and papering over it here would hide the actual bug.
            throw new ArgumentException(
                $"Balances must add up to zero, they add up to {total}.",
                nameof(balances));
        }

        // Deepest debtor and largest creditor first; ties go to the lower member id so the plan
        // does not reshuffle itself between two page loads.
        PriorityQueue<Guid, (decimal NegatedAmount, string MemberId)> debtors = new(Order);
        PriorityQueue<Guid, (decimal NegatedAmount, string MemberId)> creditors = new(Order);
        Dictionary<Guid, decimal> remaining = [];

        foreach (MemberBalance balance in balances)
        {
            remaining[balance.MemberId] = balance.Balance;

            if (balance.Owes)
            {
                debtors.Enqueue(balance.MemberId, KeyOf(-balance.Balance, balance.MemberId));
            }
            else if (balance.IsOwed)
            {
                creditors.Enqueue(balance.MemberId, KeyOf(balance.Balance, balance.MemberId));
            }
        }

        List<SettlementTransfer> plan = [];

        while (debtors.Count > 0 && creditors.Count > 0)
        {
            Guid debtor = debtors.Dequeue();
            Guid creditor = creditors.Dequeue();

            // Every step clears at least one of the two, so there can never be more than
            // N − 1 transfers, and each member is enqueued at most once more than they are removed.
            decimal amount = Math.Min(-remaining[debtor], remaining[creditor]);

            plan.Add(new SettlementTransfer(debtor, creditor, amount));
            remaining[debtor] += amount;
            remaining[creditor] -= amount;

            if (remaining[debtor] < 0m)
            {
                debtors.Enqueue(debtor, KeyOf(-remaining[debtor], debtor));
            }

            if (remaining[creditor] > 0m)
            {
                creditors.Enqueue(creditor, KeyOf(remaining[creditor], creditor));
            }
        }

        return plan;
    }

    /// <summary>Negated, because <see cref="PriorityQueue{TElement, TPriority}"/> serves the smallest first.</summary>
    private static (decimal NegatedAmount, string MemberId) KeyOf(decimal amount, Guid memberId) =>
        (-amount, memberId.ToString());

    private static readonly IComparer<(decimal NegatedAmount, string MemberId)> Order =
        Comparer<(decimal NegatedAmount, string MemberId)>.Create((left, right) =>
        {
            int byAmount = left.NegatedAmount.CompareTo(right.NegatedAmount);

            // Ordinal, not the default string comparison: the latter is culture-sensitive, and a
            // settlement plan must not depend on the server's locale.
            return byAmount != 0
                ? byAmount
                : string.CompareOrdinal(left.MemberId, right.MemberId);
        });
}
