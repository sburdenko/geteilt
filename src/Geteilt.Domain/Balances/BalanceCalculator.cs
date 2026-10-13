using Geteilt.Domain.Expenses;
using Geteilt.Domain.Settlements;

namespace Geteilt.Domain.Balances;

/// <summary>
/// Who is up and who is down, from the expenses and settlements of one group.
/// A pure function: no database, no clock, so it is testable as arrays in and arrays out — and the
/// same code runs in the browser too.
/// </summary>
public static class BalanceCalculator
{
    /// <param name="memberIds">
    /// Everyone in the group, including members marked as removed. Leaving a removed member out
    /// would drop their debts from the sum, and "all balances add up to zero" would stop holding.
    /// </param>
    public static IReadOnlyList<MemberBalance> Calculate(
        IReadOnlyCollection<Guid> memberIds,
        IReadOnlyCollection<Expense> expenses,
        IReadOnlyCollection<Settlement> settlements)
    {
        ArgumentNullException.ThrowIfNull(memberIds);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(settlements);

        Dictionary<Guid, decimal> balances = memberIds.Distinct().ToDictionary(id => id, _ => 0m);

        foreach (Expense expense in expenses.Where(expense => !expense.IsDeleted))
        {
            Add(balances, expense.PaidByMemberId, expense.AmountInBaseCurrency);

            foreach (ExpenseShare share in expense.Shares)
            {
                Add(balances, share.MemberId, -share.ShareAmountInBaseCurrency);
            }
        }

        foreach (Settlement settlement in settlements.Where(settlement => !settlement.IsDeleted))
        {
            // Paying money out moves you up, exactly as paying for an expense does. The other way
            // round a settlement would deepen the debt it repays instead of bringing everyone to zero.
            Add(balances, settlement.FromMemberId, settlement.AmountInBaseCurrency);
            Add(balances, settlement.ToMemberId, -settlement.AmountInBaseCurrency);
        }

        // Ordered by member id as text, the same tie-break Money.Allocate uses, so the result does
        // not depend on what order the database handed the rows back in.
        return
        [
            .. balances
                .Select(entry => new MemberBalance(entry.Key, entry.Value))
                .OrderBy(balance => balance.MemberId.ToString(), StringComparer.Ordinal)
        ];
    }

    private static void Add(Dictionary<Guid, decimal> balances, Guid memberId, decimal amount)
    {
        // A member who left the group still appears in old expenses; they must keep their balance
        // rather than have it silently dropped.
        balances[memberId] = balances.GetValueOrDefault(memberId) + amount;
    }
}
