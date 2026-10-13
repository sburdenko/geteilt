using Geteilt.Contracts.Balances;
using Geteilt.Web.Balances;

namespace Geteilt.Web.Tests;

/// <summary>Who gets money back, who owes it, and how to settle.</summary>
public sealed class BalanceSheetTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ThoseOwedComeFirst_ThenThoseWhoOwe_ThenThoseSquare_EachLargestFirst()
    {
        BalanceSheet sheet = Sheet(
            Balance("Cleo", -3.33m), Balance("Dora", 0m), Balance("Anna", 6.66m),
            Balance("Ben", -3.33m), Balance("Emil", 1.00m), Balance("Finn", -10.00m));

        sheet.Balances.Select(row => row.MemberName).Should().Equal("Anna", "Emil", "Finn", "Ben", "Cleo", "Dora");
    }

    [Theory]
    [InlineData(0.01, BalanceSide.GetsBack)]
    [InlineData(-0.01, BalanceSide.Owes)]
    [InlineData(0, BalanceSide.Square)]
    public void TheSign_DecidesTheSide(decimal balance, BalanceSide side)
    {
        BalanceSheet.SideOf(balance).Should().Be(side);
    }

    [Fact]
    public void Transfers_KeepTheServersOrder()
    {
        // The plan's order is the server's decision (largest first); the screen only lists it.
        PlannedTransferDto small = Transfer("Ben", "Anna", 1.00m);
        PlannedTransferDto large = Transfer("Cleo", "Anna", 9.00m);

        BalanceSheet sheet = BalanceSheet.From(
            new BalancesResponse("EUR", [], Now), new SettlementPlanResponse("EUR", [small, large], Now));

        sheet.Transfers.Should().Equal(small, large);
    }

    [Fact]
    public void TheSheetCountsInTheGroupsCurrency()
    {
        BalanceSheet.From(new BalancesResponse("CHF", [], Now), new SettlementPlanResponse("CHF", [], Now))
            .Currency.Should().Be("CHF");
    }

    private static BalanceSheet Sheet(params BalanceDto[] balances) =>
        BalanceSheet.From(new BalancesResponse("EUR", balances, Now), new SettlementPlanResponse("EUR", [], Now));

    private static BalanceDto Balance(string name, decimal balance) => new(Guid.NewGuid(), name, balance);

    private static PlannedTransferDto Transfer(string from, string to, decimal amount) =>
        new(Guid.NewGuid(), from, Guid.NewGuid(), to, amount, "EUR", null, CanGenerateQr: false);
}
