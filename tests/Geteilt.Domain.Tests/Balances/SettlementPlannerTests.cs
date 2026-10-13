using System.Globalization;
using CsCheck;
using Geteilt.Domain.Balances;

namespace Geteilt.Domain.Tests.Balances;

/// <summary>The greedy settlement plan.</summary>
public sealed class SettlementPlannerTests
{
    private static IReadOnlyList<MemberBalance> Balances(params decimal[] amounts) =>
        [.. MemberIds.Ascending(amounts.Length).Select((id, i) => new MemberBalance(id, amounts[i]))];

    private static string Describe(SettlementTransfer transfer, IReadOnlyList<MemberBalance> balances)
    {
        int from = balances.ToList().FindIndex(balance => balance.MemberId == transfer.FromMemberId);
        int to = balances.ToList().FindIndex(balance => balance.MemberId == transfer.ToMemberId);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{(char)('A' + from)}->{(char)('A' + to)} {transfer.Amount:0.00}");
    }

    [Fact]
    public void Plan_ForDinnerAndTaxi_IsASingleTransfer()
    {
        IReadOnlyList<MemberBalance> balances = Balances(30.00m, 0.00m, -30.00m);

        IReadOnlyList<SettlementTransfer> plan = SettlementPlanner.Plan(balances);

        plan.Should().ContainSingle();
        plan[0].FromMemberId.Should().Be(balances[2].MemberId);
        plan[0].ToMemberId.Should().Be(balances[0].MemberId);
        plan[0].Amount.Should().Be(30.00m);
    }

    [Fact]
    public void Plan_ForSixPeople_TakesFourTransfersInTheStatedOrder()
    {
        // A = +7, B = -4, C = -3, D = +5, E = -5
        IReadOnlyList<MemberBalance> balances = Balances(7.00m, -4.00m, -3.00m, 5.00m, -5.00m);

        IReadOnlyList<SettlementTransfer> plan = SettlementPlanner.Plan(balances);

        plan.Select(transfer => Describe(transfer, balances))
            .Should().Equal("E->A 5.00", "B->D 4.00", "C->A 2.00", "C->D 1.00");
    }

    [Fact]
    public void Plan_WhenEverybodyIsSquare_IsEmpty()
    {
        SettlementPlanner.Plan(Balances(0.00m, 0.00m, 0.00m)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_IsDeterministic()
    {
        IReadOnlyList<MemberBalance> balances = Balances(7.00m, -4.00m, -3.00m, 5.00m, -5.00m);

        SettlementPlanner.Plan(balances).Should().Equal(SettlementPlanner.Plan(balances));
    }

    [Fact]
    public void Plan_OnEqualAmounts_BreaksTheTieOnTheLowerMemberId()
    {
        // Two creditors owed the same: the debtor must pay the lower id first, every time.
        IReadOnlyList<MemberBalance> balances = Balances(5.00m, 5.00m, -10.00m);

        IReadOnlyList<SettlementTransfer> plan = SettlementPlanner.Plan(balances);

        plan[0].ToMemberId.Should().Be(balances[0].MemberId);
    }

    [Fact]
    public void Plan_WhenBalancesDoNotSumToZero_Throws()
    {
        Action act = () => SettlementPlanner.Plan(Balances(10.00m, -5.00m));

        act.Should().ThrowExactly<ArgumentException>();
    }

    private static readonly Gen<decimal[]> BalanceVectors =
        Gen.Int[2, 12].SelectMany(count =>
            Gen.Long[-100_000, 100_000].Array[count].Select(cents =>
            {
                decimal[] balances = [.. cents.Select(value => value / 100m)];
                // Force the vector to sum to zero by absorbing the remainder into the last entry.
                balances[^1] -= balances.Sum();
                return balances;
            }));

    [Fact]
    public void Plan_ForAnyBalanceVector_NeedsAtMostOneTransferFewerThanThereAreMembers()
    {
        BalanceVectors.Sample(
            amounts =>
            {
                IReadOnlyList<MemberBalance> balances = Balances(amounts);

                SettlementPlanner.Plan(balances).Count.Should().BeLessThan(balances.Count);
            },
            iter: 10_000);
    }

    [Fact]
    public void Plan_ForAnyBalanceVector_LeavesEverybodyAtZero()
    {
        BalanceVectors.Sample(
            amounts =>
            {
                IReadOnlyList<MemberBalance> balances = Balances(amounts);
                Dictionary<Guid, decimal> remaining = balances.ToDictionary(b => b.MemberId, b => b.Balance);

                foreach (SettlementTransfer transfer in SettlementPlanner.Plan(balances))
                {
                    transfer.Amount.Should().BePositive();
                    remaining[transfer.FromMemberId] += transfer.Amount;
                    remaining[transfer.ToMemberId] -= transfer.Amount;
                }

                remaining.Values.Should().AllSatisfy(balance => balance.Should().Be(0.00m));
            },
            iter: 1000);
    }
}
