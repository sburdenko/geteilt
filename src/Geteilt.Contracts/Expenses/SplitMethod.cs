namespace Geteilt.Contracts.Expenses;

/// <summary>
/// How an expense is divided.
/// </summary>
/// <remarks>
/// A second copy of the domain enum, for the same reason as MemberRole: Domain and Contracts each
/// reference nothing, so a shared type has nowhere to live.
/// </remarks>
public enum SplitMethod
{
    /// <summary>Evenly. The odd cent goes to the lowest member id.</summary>
    Equal,

    /// <summary>A figure per person, which must add up to the total exactly.</summary>
    ExactAmounts,

    /// <summary>Whole shares, 2:1:1.</summary>
    Shares,

    /// <summary>Percentages, which must add up to 100.00.</summary>
    Percentages,
}
