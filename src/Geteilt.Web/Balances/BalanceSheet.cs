using Geteilt.Contracts.Balances;

namespace Geteilt.Web.Balances;

/// <summary>
/// The balances screen in one piece: everyone's balance in reading order, and the transfers that
/// settle them. Built only when both answers arrived, so the screen never shows balances next to
/// a plan from a different moment.
/// </summary>
/// <param name="Transfers">In the server's order, which puts the largest first.</param>
public sealed record BalanceSheet(
    string Currency,
    IReadOnlyList<BalanceDto> Balances,
    IReadOnlyList<PlannedTransferDto> Transfers)
{
    public static BalanceSheet From(BalancesResponse balances, SettlementPlanResponse plan)
    {
        ArgumentNullException.ThrowIfNull(balances);
        ArgumentNullException.ThrowIfNull(plan);

        BalanceDto[] inReadingOrder =
        [
            .. balances.Balances
                .OrderBy(row => SideOf(row.Balance))
                .ThenByDescending(row => Math.Abs(row.Balance))
                .ThenBy(row => row.MemberName, StringComparer.CurrentCulture)
        ];

        return new BalanceSheet(balances.BaseCurrency, inReadingOrder, plan.Transfers);
    }

    /// <summary>Positive means the group owes them, negative that they owe the group.</summary>
    public static BalanceSide SideOf(decimal balance) => balance switch
    {
        > 0 => BalanceSide.GetsBack,
        < 0 => BalanceSide.Owes,
        _ => BalanceSide.Square,
    };
}
