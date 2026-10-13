namespace Geteilt.Infrastructure.Persistence.Configurations;

/// <summary>
/// Column types for the two kinds of number in this system. Written once so no configuration can
/// quietly disagree — a money column that ended up as <c>numeric</c> without a scale would round
/// differently from every other one.
/// </summary>
internal static class MoneyColumns
{
    /// <summary>Money. Two decimal places, never more.</summary>
    internal const string Amount = "numeric(18,2)";

    /// <summary>Exchange rates and split weights. Not money, so six places.</summary>
    internal const string Rate = "numeric(18,6)";
}
