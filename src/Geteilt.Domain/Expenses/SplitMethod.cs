namespace Geteilt.Domain.Expenses;

/// <summary>How an expense amount is turned into a share per member.</summary>
public enum SplitMethod
{
    /// <summary>Same share for everyone, odd cents handed out by <see cref="Money.Allocate"/>.</summary>
    Equal,

    /// <summary>The user typed an amount per member; they must add up to the total exactly.</summary>
    ExactAmounts,

    /// <summary>Whole shares such as 2:1:1.</summary>
    Shares,

    /// <summary>Percentages that must add up to 100.00.</summary>
    Percentages,

    /// <summary>Receipt lines assigned to members. Reserved in the contract; not accepted yet.</summary>
    ByItems,
}
