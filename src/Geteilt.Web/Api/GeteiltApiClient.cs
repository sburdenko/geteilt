namespace Geteilt.Web.Api;

/// <summary>
/// The typed API client, split by resource so no one file grows to hold the whole
/// API. Pages may inject either this or a single section.
/// </summary>
public sealed class GeteiltApiClient(
    GroupsApi groups,
    MembersApi members,
    ExpensesApi expenses,
    BalancesApi balances,
    SettlementsApi settlements,
    GirocodeApi girocode)
{
    public GroupsApi Groups { get; } = groups;

    public MembersApi Members { get; } = members;

    public ExpensesApi Expenses { get; } = expenses;

    public BalancesApi Balances { get; } = balances;

    public SettlementsApi Settlements { get; } = settlements;

    public GirocodeApi Girocode { get; } = girocode;
}
