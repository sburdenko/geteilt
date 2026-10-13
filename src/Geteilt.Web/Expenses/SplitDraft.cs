using System.Collections.Immutable;

namespace Geteilt.Web.Expenses;

/// <summary>
/// How the bill is being divided while somebody is still typing: evenly among the people ticked,
/// or by figures they placed themselves.
/// </summary>
/// <remarks>
/// Closed rather than a method enum plus a nullable amount per person. The two cases hold
/// genuinely different data — a set of people against a figure for each — and the illegal
/// combinations (an even split carrying amounts, an exact split without them) then have nowhere
/// to exist. <c>ShareCalculator</c> rejects an even split that arrives with values, so those
/// combinations are not theoretical.
/// </remarks>
public abstract record SplitDraft
{
    private SplitDraft()
    {
    }

    public abstract IImmutableSet<Guid> Participants { get; }

    public sealed record Evenly(IImmutableSet<Guid> Participants) : SplitDraft
    {
        public override IImmutableSet<Guid> Participants { get; } = Participants;
    }

    public sealed record Exactly(IImmutableDictionary<Guid, decimal> Amounts) : SplitDraft
    {
        public override IImmutableSet<Guid> Participants { get; } = Amounts.Keys.ToImmutableHashSet();
    }

    public static SplitDraft Nobody { get; } = new Evenly(ImmutableHashSet<Guid>.Empty);
}
