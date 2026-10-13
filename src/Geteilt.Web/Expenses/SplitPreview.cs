namespace Geteilt.Web.Expenses;

/// <param name="Amount">What this person owes, to the cent.</param>
/// <param name="CarriesTheOddCent">
/// True when an even split could not divide cleanly and this person took one cent more than the
/// smallest share. Shown as a marker rather than hidden, because a total that divides unevenly is
/// the moment people start recounting by hand.
/// </param>
public sealed record MemberShare(Guid MemberId, decimal Amount, bool CarriesTheOddCent);

/// <param name="Remaining">
/// Total minus what has been placed. Zero for an even split by construction; for exact amounts it
/// is what the person still has to account for, and it may go negative when they overshoot.
/// </param>
/// <param name="AddsUp">
/// Whether the split accounts for the whole bill. Carried rather than derived from
/// <paramref name="Remaining"/>: a form with nothing typed in it also has nothing left over, and
/// deriving would have it announce "Rest 0,00 ✓" over three empty rows — claiming the split is
/// settled before there is anything to settle.
/// </param>
public sealed record SplitPreview(IReadOnlyList<MemberShare> Shares, decimal Remaining, bool AddsUp);
