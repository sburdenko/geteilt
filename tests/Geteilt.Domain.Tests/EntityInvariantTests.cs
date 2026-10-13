using Geteilt.Domain.Expenses;
using Geteilt.Domain.Groups;
using Geteilt.Domain.Members;
using Geteilt.Domain.Settlements;
using Geteilt.Domain.Users;

namespace Geteilt.Domain.Tests;

/// <summary>Every invariant an entity refuses to be constructed without.</summary>
public sealed class EntityInvariantTests
{
    private static readonly Guid GroupId = new("00000000-0000-0000-0000-0000000000aa");
    private static readonly Guid ExpenseId = new("00000000-0000-0000-0000-0000000000e1");
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 8, 15);
    private static readonly IReadOnlyList<Guid> Trio = MemberIds.Ascending(3);

    private static NewExpense Draft(decimal amount = 30.00m) => new()
    {
        Id = ExpenseId,
        GroupId = GroupId,
        Description = "Abendessen",
        PaidByMemberId = Trio[0],
        Amount = amount,
        Currency = "EUR",
        Date = Today,
        SplitMethod = SplitMethod.Equal,
        CreatedByMemberId = Trio[0],
        CreatedAt = Now,
    };

    private static IReadOnlyList<ExpenseShare> EvenShares(decimal amount) =>
        ShareCalculator.Calculate(ExpenseId, amount, SplitMethod.Equal, [.. Trio.Select(id => new ShareInput(id))]);

    private static Expense CreateWith(NewExpense draft, IReadOnlyList<ExpenseShare> shares) =>
        Expense.Create(draft, shares, [.. Trio], "EUR");

    [Theory]
    [MemberData(nameof(NonPositiveAmounts))]
    public void Expense_WithAnAmountThatIsNotPositive_IsRejected(decimal amount)
    {
        Action act = () => CreateWith(Draft(amount), EvenShares(30.00m));

        act.Should().ThrowExactly<ExpenseException>()
            .Which.Code.Should().Be(ExpenseException.AmountMustBePositive.Code);
    }

    public static TheoryData<decimal> NonPositiveAmounts() => new() { 0m, -0.01m, -30.00m };

    [Fact]
    public void Expense_WhereSharesDoNotAddUpToTheAmount_IsRejected()
    {
        Action act = () => CreateWith(Draft(30.00m), EvenShares(29.99m));

        act.Should().ThrowExactly<ExpenseException>()
            .Which.Code.Should().Be(ExpenseException.SharesSumMismatch.Code);
    }

    [Fact]
    public void Expense_WhereThePayerIsNotInTheGroup_IsRejected()
    {
        Guid outsider = new("00000000-0000-0000-0000-0000000000ff");

        Action act = () => CreateWith(Draft() with { PaidByMemberId = outsider }, EvenShares(30.00m));

        act.Should().ThrowExactly<ExpenseException>()
            .Which.Code.Should().Be(ExpenseException.MemberNotInGroup.Code);
    }

    [Fact]
    public void Expense_DatedMoreThanADayAhead_IsRejected()
    {
        Action act = () => CreateWith(Draft() with { Date = Today.AddDays(2) }, EvenShares(30.00m));

        act.Should().ThrowExactly<ExpenseException>()
            .Which.Code.Should().Be(ExpenseException.DateInFuture.Code);
    }

    [Fact]
    public void Expense_DatedTomorrow_IsAccepted()
    {
        // A phone in a timezone ahead of UTC must not have its expense refused.
        Action act = () => CreateWith(Draft() with { Date = Today.AddDays(1) }, EvenShares(30.00m));

        act.Should().NotThrow();
    }

    [Fact]
    public void Expense_InACurrencyOtherThanTheGroupBase_IsRejectedUntilM4()
    {
        Action act = () => Expense.Create(Draft() with { Currency = "USD" }, EvenShares(30.00m), [.. Trio], "EUR");

        act.Should().ThrowExactly<ExpenseException>()
            .Which.Code.Should().Be(ExpenseException.CurrencyMismatch.Code);
    }

    [Fact]
    public void Expense_ExposesItsSharesAsAReadOnlyCollection()
    {
        Expense expense = CreateWith(Draft(), EvenShares(30.00m));

        expense.Shares.Should().HaveCount(3);
        expense.Shares.Should().BeAssignableTo<IReadOnlyCollection<ExpenseShare>>();
        expense.Shares.Sum(share => share.ShareAmount).Should().Be(expense.Amount);
    }

    [Fact]
    public void Settlement_FromAMemberToThemselves_IsRejected()
    {
        Action act = () => Settlement.Create(
            Guid.CreateVersion7(), GroupId, Trio[0], Trio[0], 10.00m, "EUR", Today, Now);

        act.Should().ThrowExactly<SettlementException>()
            .Which.Code.Should().Be(SettlementException.SelfTransfer.Code);
    }

    [Theory]
    [MemberData(nameof(NonPositiveAmounts))]
    public void Settlement_WithAnAmountThatIsNotPositive_IsRejected(decimal amount)
    {
        Action act = () => Settlement.Create(
            Guid.CreateVersion7(), GroupId, Trio[0], Trio[1], amount, "EUR", Today, Now);

        act.Should().ThrowExactly<SettlementException>()
            .Which.Code.Should().Be(SettlementException.AmountMustBePositive.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Member_WithoutADisplayName_IsRejected(string displayName)
    {
        Action act = () => _ = new Member(Guid.CreateVersion7(), GroupId, displayName, Now);

        act.Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void Member_WithAnInvalidIban_IsRejected()
    {
        Action act = () => _ = new Member(Guid.CreateVersion7(), GroupId, "Anna", Now, iban: "DE89370400440532013001");

        // The same reasoning as for User: an IBAN arrives from a form, so the API owes 422
        // invalid_iban rather than a 500.
        act.Should().ThrowExactly<DomainValidationException>()
            .Which.Code.Should().Be("invalid_iban");
    }

    [Fact]
    public void Member_StoresTheIbanInTheShapeAGirocodeNeeds()
    {
        Member member = new(Guid.CreateVersion7(), GroupId, "Anna", Now, iban: "de89 3704 0044 0532 0130 00");

        member.Iban.Should().Be("DE89370400440532013000");
    }

    [Fact]
    public void Member_RemovedFromTheGroup_KeepsTheFirstRemovalTime()
    {
        Member member = new(Guid.CreateVersion7(), GroupId, "Anna", Now);

        member.Remove(Now);
        member.Remove(Now.AddDays(1));

        member.RemovedAt.Should().Be(Now);
        member.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Group_WithACurrencyThatIsNotIso4217_IsRejected()
    {
        Action act = () => _ = new Group(GroupId, "Italien 2026", "EURO", Guid.CreateVersion7(), Now);

        // A domain exception carrying its error code, not an ArgumentException: the currency
        // arrives in a request body, so the API owes the caller 422 invalid_currency, not a 500.
        act.Should().ThrowExactly<DomainValidationException>()
            .Which.Code.Should().Be("invalid_currency");
    }

    [Fact]
    public void User_StoresTheEmailInLowerCase()
    {
        User user = new(Guid.CreateVersion7(), "Anna@Example.COM", "Anna", Now);

        user.Email.Should().Be("anna@example.com");
    }
}
