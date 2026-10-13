using Geteilt.Contracts.Groups;
using Geteilt.Contracts.Members;
using Geteilt.Web.Expenses;

namespace Geteilt.Web.Tests;

/// <summary>The expense form exists only once its group and members have both arrived.</summary>
public sealed class ExpenseFormTests
{
    private static readonly Guid AnnaUser = Guid.Parse("00000000-0000-0000-0000-00000000aaaa");
    private static readonly DateOnly Today = new(2026, 10, 9);

    private static readonly GroupDto Trip = new(
        Guid.Parse("00000000-0000-0000-0000-0000000000f1"), "Trip", "CHF", DateTimeOffset.UnixEpoch, null, 3, 0m);

    private static readonly MemberDto Anna = Member("a1", "Anna", AnnaUser);
    private static readonly MemberDto Boris = Member("b2", "Boris", null);
    private static readonly MemberDto LeftLastYear = Member("c3", "Clara", null) with { RemovedAt = DateTimeOffset.UnixEpoch };

    [Fact]
    public void TheFormCountsInTheGroupsCurrency()
    {
        ExpenseForm.Open(Trip, [Anna, Boris], AnnaUser, Today).Currency.Should().Be("CHF");
    }

    [Fact]
    public void SomeoneWhoLeft_TakesNoPartInNewExpenses()
    {
        ExpenseForm form = ExpenseForm.Open(Trip, [Anna, Boris, LeftLastYear], AnnaUser, Today);

        form.Members.Should().Equal(Anna, Boris);
        form.Draft.Split.Participants.Should().BeEquivalentTo([Anna.Id, Boris.Id]);
    }

    [Fact]
    public void ThePersonLookingAtTheScreen_IsGuessedAsThePayer()
    {
        ExpenseForm.Open(Trip, [Anna, Boris], AnnaUser, Today).Draft.PaidByMemberId.Should().Be(Anna.Id);
    }

    [Fact]
    public void AViewerWhoIsNoMember_LeavesThePayerOpen()
    {
        ExpenseForm.Open(Trip, [Anna, Boris], Guid.NewGuid(), Today).Draft.PaidByMemberId.Should().BeNull();
    }

    [Fact]
    public void With_ReplacesTheDraftAndKeepsTheRest()
    {
        ExpenseForm form = ExpenseForm.Open(Trip, [Anna, Boris], AnnaUser, Today);

        ExpenseForm edited = form.With(form.Draft.WithAmount(12.30m));

        edited.Draft.Amount.Should().Be(12.30m);
        edited.Currency.Should().Be(form.Currency);
        form.Draft.Amount.Should().BeNull("the original form is not changed");
    }

    private static MemberDto Member(string suffix, string name, Guid? userId) => new(
        Guid.Parse($"00000000-0000-0000-0000-0000000000{suffix}"), userId, name, null, MemberRole.Member,
        userId is not null, null);
}
