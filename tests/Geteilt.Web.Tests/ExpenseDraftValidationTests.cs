using Geteilt.Domain.Expenses;
using Geteilt.Web.Expenses;

namespace Geteilt.Web.Tests;

public sealed class ExpenseDraftValidationTests
{
    private static readonly Guid Anna = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Boris = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly DateOnly Today = new(2026, 8, 17);

    private static ExpenseDraft Ready() => ExpenseDraft.ForGroup(Today, [Anna, Boris])
        .WithDescription("Wein & Wasser")
        .WithAmount(36.90m)
        .WithPayer(Anna);

    [Fact]
    public void ACompleteDraft_CanBeSent() => Ready().CanBeSent(Today).Should().BeTrue();

    [Fact]
    public void ABlankDraft_NamesEverythingMissingAtOnce()
    {
        // All of it, not the first one. A form that reveals its objections one at a time makes
        // the person press Save once per mistake.
        IReadOnlyList<DraftProblem> problems = ExpenseDraft.ForGroup(Today, []).Problems(Today);

        problems.Should().BeEquivalentTo(new[]
        {
            DraftProblem.DescriptionMissing,
            DraftProblem.AmountMustBePositive,
            DraftProblem.PayerMissing,
            DraftProblem.NobodyTakingPart,
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ADescriptionOfNothing_IsMissing(string description) =>
        Ready().WithDescription(description).Problems(Today).Should().Contain(DraftProblem.DescriptionMissing);

    [Fact]
    public void ADescriptionPastTheServersLimit_IsRejectedHere_Too() =>
        Ready().WithDescription(new string('x', ExpenseDraft.MaxDescriptionLength + 1))
            .Problems(Today).Should().Contain(DraftProblem.DescriptionTooLong);

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void AnAmountThatIsNotPositive_IsRejected(double? amount) =>
        Ready().WithAmount((decimal?)amount).Problems(Today).Should().Contain(DraftProblem.AmountMustBePositive);

    [Fact]
    public void Tomorrow_IsAllowed_BecauseTheServerAllowsIt() =>
        Ready().WithDate(Today.AddDays(1)).CanBeSent(Today).Should().BeTrue();

    [Fact]
    public void TheDayAfter_IsNot() =>
        Ready().WithDate(Today.AddDays(2)).Problems(Today).Should().Contain(DraftProblem.DateInFuture);

    [Fact]
    public void AnEvenSplitNeverFailsOnTheSum() =>
        Ready().WithAmount(10.00m).Problems(Today).Should().NotContain(DraftProblem.SharesSumMismatch);

    [Theory]
    [InlineData(20.00, 16.90, true)]
    [InlineData(20.00, 16.91, false)]
    [InlineData(18.45, 18.45, true)]
    [InlineData(0.00, 36.90, true)]
    public void AnExactSplit_AgreesWithTheServerOnWhetherItAddsUp(
        double anna,
        double boris,
        bool expected)
    {
        // The same question asked twice: once by the form, once by the code the API runs. A form
        // that says yes where ShareCalculator says no is a form that throws away the typing.
        ExpenseDraft draft = Ready()
            .WithExactAmount(Anna, (decimal)anna)
            .WithExactAmount(Boris, (decimal)boris);

        bool formAccepts = !draft.Problems(Today).Contains(DraftProblem.SharesSumMismatch);
        bool serverAccepts = ServerAccepts(draft);

        formAccepts.Should().Be(expected);
        serverAccepts.Should().Be(expected);
    }

    private static bool ServerAccepts(ExpenseDraft draft)
    {
        try
        {
            ShareCalculator.Calculate(
                Guid.NewGuid(),
                draft.Amount!.Value,
                SplitMethod.ExactAmounts,
                [.. draft.Preview().Shares.Select(share => new ShareInput(share.MemberId, share.Amount))]);

            return true;
        }
        catch (ExpenseException)
        {
            return false;
        }
    }
}
