using CsCheck;
using Geteilt.Domain.Balances;
using Geteilt.Domain.Expenses;
using Geteilt.Domain.Members;
using Geteilt.Domain.Settlements;

namespace Geteilt.Domain.Tests.Balances;

/// <summary>Balances: paid − owed + sent − received, always adding up to zero.</summary>
public sealed class BalanceCalculatorTests
{
    private static readonly IReadOnlyList<Guid> Trio = MemberIds.Ascending(3);
    private static Guid Anna => Trio[0];
    private static Guid Boris => Trio[1];
    private static Guid Clara => Trio[2];

    private static readonly Guid GroupId = new("00000000-0000-0000-0000-0000000000aa");
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 8, 15);

    private static Expense EqualExpense(string description, Guid paidBy, decimal amount, Guid expenseId)
    {
        NewExpense draft = new()
        {
            Id = expenseId,
            GroupId = GroupId,
            Description = description,
            PaidByMemberId = paidBy,
            Amount = amount,
            Currency = "EUR",
            Date = Today,
            SplitMethod = SplitMethod.Equal,
            CreatedByMemberId = paidBy,
            CreatedAt = Now,
        };

        IReadOnlyList<ExpenseShare> shares = ShareCalculator.Calculate(
            expenseId, amount, SplitMethod.Equal, [.. Trio.Select(id => new ShareInput(id))]);

        return Expense.Create(draft, shares, [.. Trio], "EUR");
    }

    /// <summary>Anna pays 60 for dinner, Boris 30 for the taxi, both split three ways.</summary>
    private static Expense[] DinnerAndTaxi() =>
    [
        EqualExpense("Abendessen", Anna, 60.00m, new Guid("00000000-0000-0000-0000-0000000000e1")),
        EqualExpense("Taxi", Boris, 30.00m, new Guid("00000000-0000-0000-0000-0000000000e2")),
    ];

    private static decimal BalanceOf(IReadOnlyList<MemberBalance> balances, Guid memberId) =>
        balances.Single(balance => balance.MemberId == memberId).Balance;

    [Fact]
    public void Calculate_ForDinnerAndTaxi_GivesPlusThirtyZeroMinusThirty()
    {
        IReadOnlyList<MemberBalance> balances = BalanceCalculator.Calculate(Trio, DinnerAndTaxi(), []);

        BalanceOf(balances, Anna).Should().Be(30.00m);
        BalanceOf(balances, Boris).Should().Be(0.00m);
        BalanceOf(balances, Clara).Should().Be(-30.00m);
    }

    [Fact]
    public void Calculate_AfterTheDinnerAndTaxiSettlement_LeavesEverythingAtZero()
    {
        Settlement settled = Settlement.Create(
            Guid.CreateVersion7(), GroupId, Clara, Anna, 30.00m, "EUR", Today, Now);

        IReadOnlyList<MemberBalance> balances = BalanceCalculator.Calculate(Trio, DinnerAndTaxi(), [settled]);

        balances.Should().AllSatisfy(balance => balance.Balance.Should().Be(0.00m));
    }

    [Fact]
    public void Calculate_IgnoresASoftDeletedExpense()
    {
        Expense[] expenses = DinnerAndTaxi();
        expenses[0].Delete(Now);

        IReadOnlyList<MemberBalance> balances = BalanceCalculator.Calculate(Trio, expenses, []);

        // Only the taxi is left: Boris paid 30 and owes 10 of it.
        BalanceOf(balances, Anna).Should().Be(-10.00m);
        BalanceOf(balances, Boris).Should().Be(20.00m);
        BalanceOf(balances, Clara).Should().Be(-10.00m);
    }

    [Fact]
    public void Calculate_IgnoresASoftDeletedSettlement()
    {
        Settlement settled = Settlement.Create(
            Guid.CreateVersion7(), GroupId, Clara, Anna, 30.00m, "EUR", Today, Now);
        settled.Delete(Now);

        IReadOnlyList<MemberBalance> balances = BalanceCalculator.Calculate(Trio, DinnerAndTaxi(), [settled]);

        BalanceOf(balances, Anna).Should().Be(30.00m);
    }

    [Fact]
    public void Calculate_StillCountsAMemberWhoWasRemovedFromTheGroup()
    {
        // Removal is bookkeeping, not erasure: drop Clara and the balances stop summing to zero.
        Member clara = new(Clara, GroupId, "Clara", Now);
        clara.Remove(Now);

        IReadOnlyList<MemberBalance> balances = BalanceCalculator.Calculate(Trio, DinnerAndTaxi(), []);

        BalanceOf(balances, clara.Id).Should().Be(-30.00m);
        balances.Sum(balance => balance.Balance).Should().Be(0.00m);
    }

    [Fact]
    public void Calculate_ListsAMemberWithNoActivityAtZeroRatherThanOmittingThem()
    {
        Guid[] withNewcomer = [.. Trio, new Guid("00000000-0000-0000-0000-000000000009")];

        IReadOnlyList<MemberBalance> balances = BalanceCalculator.Calculate(withNewcomer, DinnerAndTaxi(), []);

        balances.Should().HaveCount(4);
        BalanceOf(balances, withNewcomer[3]).Should().Be(0.00m);
    }

    [Fact]
    public void Calculate_IsDeterministic()
    {
        IReadOnlyList<MemberBalance> first = BalanceCalculator.Calculate(Trio, DinnerAndTaxi(), []);
        IReadOnlyList<MemberBalance> second = BalanceCalculator.Calculate([.. Trio.Reverse()], DinnerAndTaxi(), []);

        second.Should().Equal(first);
    }

    [Fact]
    public void Calculate_ForAnyGroupAndExpenses_BalancesSumToZero()
    {
        Gen.Select(Gen.Int[2, 6], Gen.Long[1, 100_000].Array[1, 8], Gen.Int[0, 5]).Sample(
            (memberCount, amountsInCents, payerSeed) =>
            {
                IReadOnlyList<Guid> members = MemberIds.Ascending(memberCount);
                Expense[] expenses =
                [
                    .. amountsInCents.Select((cents, i) => EqualExpenseFor(
                        members,
                        members[(payerSeed + i) % memberCount],
                        cents / 100m,
                        i))
                ];

                IReadOnlyList<MemberBalance> balances = BalanceCalculator.Calculate(members, expenses, []);

                balances.Sum(balance => balance.Balance).Should().Be(0.00m);
            },
            iter: 1000);
    }

    private static Expense EqualExpenseFor(IReadOnlyList<Guid> members, Guid paidBy, decimal amount, int index)
    {
        Guid expenseId = new($"00000000-0000-0000-0000-{index:D12}");

        NewExpense draft = new()
        {
            Id = expenseId,
            GroupId = GroupId,
            Description = "generated",
            PaidByMemberId = paidBy,
            Amount = amount,
            Currency = "EUR",
            Date = Today,
            SplitMethod = SplitMethod.Equal,
            CreatedByMemberId = paidBy,
            CreatedAt = Now,
        };

        IReadOnlyList<ExpenseShare> shares = ShareCalculator.Calculate(
            expenseId, amount, SplitMethod.Equal, [.. members.Select(id => new ShareInput(id))]);

        return Expense.Create(draft, shares, [.. members], "EUR");
    }
}
