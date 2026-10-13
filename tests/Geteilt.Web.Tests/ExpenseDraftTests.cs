using System.Collections.Immutable;
using Geteilt.Contracts.Expenses;
using Geteilt.Web.Expenses;

namespace Geteilt.Web.Tests;

public sealed class ExpenseDraftTests
{
    private static readonly Guid Anna = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Boris = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Guid Clara = Guid.Parse("00000000-0000-0000-0000-0000000000c3");
    private static readonly DateOnly Today = new(2026, 8, 17);

    private static ExpenseDraft Draft() => ExpenseDraft.ForGroup(Today, [Anna, Boris, Clara]);

    [Fact]
    public void ATotalThatDividesCleanly_SplitsWithoutAnybodyCarryingExtra()
    {
        SplitPreview preview = Draft().WithAmount(36.90m).Preview();

        preview.Shares.Should().OnlyContain(share => share.Amount == 12.30m);
        preview.Shares.Should().OnlyContain(share => !share.CarriesTheOddCent);
        preview.AddsUp.Should().BeTrue();
    }

    [Fact]
    public void ATotalThatDoesNot_StillAddsUp_AndSaysWhoCarriesTheCent()
    {
        // Ten euro over three. total/n would show 3,33 three times and quietly lose a cent next
        // to a screen promising the remainder is zero.
        SplitPreview preview = Draft().WithAmount(10.00m).Preview();

        preview.Shares.Sum(share => share.Amount).Should().Be(10.00m);
        preview.Shares.Count(share => share.CarriesTheOddCent).Should().Be(1);
        preview.Shares.Single(share => share.CarriesTheOddCent).Amount.Should().Be(3.34m);
        preview.AddsUp.Should().BeTrue();
    }

    [Fact]
    public void TheOddCent_GoesWhereTheServerWouldPutIt()
    {
        // Same tie-break as Money.Allocate: member id as ordinal text. Not an implementation
        // detail on screen — the person holding the extra cent is named, and naming a different
        // one than the receipt would be worse than naming none.
        SplitPreview preview = Draft().WithAmount(10.00m).Preview();

        preview.Shares.Single(share => share.CarriesTheOddCent).MemberId.Should().Be(Anna);
    }

    [Fact]
    public void RemovingSomebody_ResplitsOverWhoIsLeft()
    {
        SplitPreview preview = Draft().WithAmount(10.00m).Toggle(Clara).Preview();

        preview.Shares.Should().HaveCount(2);
        preview.Shares.Should().OnlyContain(share => share.Amount == 5.00m);
    }

    [Fact]
    public void TypingAnAmount_TurnsAnEvenSplitIntoAnExactOne_KeepingWhatWasOnScreen()
    {
        // The first keystroke must not blank the other two rows: what was on screen was a real
        // proposal, and the person is adjusting it, not starting over.
        ExpenseDraft draft = Draft().WithAmount(36.90m).WithExactAmount(Anna, 20.00m);

        draft.Split.Should().BeOfType<SplitDraft.Exactly>();

        SplitPreview preview = draft.Preview();
        preview.Shares.Single(share => share.MemberId == Anna).Amount.Should().Be(20.00m);
        preview.Shares.Where(share => share.MemberId != Anna)
            .Should().OnlyContain(share => share.Amount == 12.30m);
    }

    [Fact]
    public void AnExactSplitThatDoesNotAddUp_SaysHowMuchIsLeft()
    {
        SplitPreview preview = Draft().WithAmount(36.90m).WithExactAmount(Anna, 20.00m).Preview();

        preview.Remaining.Should().Be(-7.70m, "20 + 12,30 + 12,30 overshoots 36,90");
        preview.AddsUp.Should().BeFalse();
    }

    [Fact]
    public void GoingBackToAnEvenSplit_ForgetsTheFigures()
    {
        ExpenseDraft draft = Draft()
            .WithAmount(36.90m)
            .WithExactAmount(Anna, 20.00m)
            .SplitEvenly();

        draft.Split.Should().BeOfType<SplitDraft.Evenly>();
        draft.Preview().Shares.Should().OnlyContain(share => share.Amount == 12.30m);
    }

    [Fact]
    public void AnEvenSplit_SendsNoPerPersonAmounts()
    {
        // ShareCalculator rejects an even split that arrives carrying values, so sending the
        // preview figures would turn a valid form into a 422.
        CreateExpenseRequest request = Draft()
            .WithAmount(36.90m).WithPayer(Anna).WithDescription("Wein & Wasser")
            .ToRequest("EUR");

        request.SplitMethod.Should().Be(SplitMethod.Equal);
        request.Shares.Should().OnlyContain(share => share.Amount == null && share.Weight == null);
        request.Shares.Should().HaveCount(3);
    }

    [Fact]
    public void AnExactSplit_SendsTheFiguresAsTyped()
    {
        CreateExpenseRequest request = Draft()
            .WithAmount(36.90m).WithPayer(Anna).WithDescription("Wein & Wasser")
            .WithExactAmount(Anna, 20.00m)
            .ToRequest("EUR");

        request.SplitMethod.Should().Be(SplitMethod.ExactAmounts);
        request.Shares.Single(share => share.MemberId == Anna).Amount.Should().Be(20.00m);
        request.Shares.Sum(share => share.Amount!.Value).Should().Be(44.60m);
    }

    [Fact]
    public void ADraftIsNeverChangedInPlace()
    {
        ExpenseDraft original = Draft().WithAmount(36.90m);

        original.WithAmount(10.00m);
        original.Toggle(Anna);
        original.WithExactAmount(Boris, 1.00m);

        original.Amount.Should().Be(36.90m);
        original.Split.Participants.Should().HaveCount(3);
        original.Split.Should().BeOfType<SplitDraft.Evenly>();
    }

    [Fact]
    public void AnEmptyGroupOrNoAmount_PreviewsWithoutThrowing()
    {
        // The form renders before anything is typed, and Money.Allocate rejects a zero total.
        Draft().Preview().Shares.Should().OnlyContain(share => share.Amount == 0m);
        ExpenseDraft.ForGroup(Today, []).WithAmount(10m).Preview().Shares.Should().BeEmpty();
    }

    [Fact]
    public void AFormWithNothingTypedIn_DoesNotClaimTheSplitIsSettled()
    {
        // Nothing entered also has nothing left over, so a remainder of zero would light the green
        // tick over three empty rows. The screenshot of the first build showed exactly that.
        Draft().Preview().AddsUp.Should().BeFalse();
        Draft().WithAmount(0m).Preview().AddsUp.Should().BeFalse();
        Draft().WithAmount(36.90m).Preview().AddsUp.Should().BeTrue();
    }

    [Fact]
    public void AnExactSplitOverAnEmptyTotal_DoesNotClaimItEither()
    {
        ExpenseDraft draft = Draft().WithAmount(10m).WithExactAmount(Anna, 0m)
            .Toggle(Boris).Toggle(Clara).WithAmount(null);

        draft.Preview().AddsUp.Should().BeFalse();
    }
}
