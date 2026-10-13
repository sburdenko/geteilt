using Geteilt.Domain.Expenses;

namespace Geteilt.Domain.Tests.Expenses;

/// <summary>Shares by split method — one method per section.</summary>
public sealed class ShareCalculatorTests
{
    private static readonly Guid ExpenseId = new("00000000-0000-0000-0000-0000000000ff");

    private static IReadOnlyList<ShareInput> ForMembers(int count) =>
        [.. MemberIds.Ascending(count).Select(id => new ShareInput(id))];

    private static IReadOnlyList<ShareInput> Weighted(params decimal[] values) =>
        [.. MemberIds.Ascending(values.Length).Select((id, i) => new ShareInput(id, values[i]))];

    private static decimal[] AmountsOf(IReadOnlyList<ExpenseShare> shares) =>
        [.. shares.Select(share => share.ShareAmount)];

    [Fact]
    public void Equal_SplitsThroughMoneyAllocate()
    {
        IReadOnlyList<ExpenseShare> shares =
            ShareCalculator.Calculate(ExpenseId, 10.00m, SplitMethod.Equal, ForMembers(3));

        AmountsOf(shares).Should().Equal(3.34m, 3.33m, 3.33m);
    }

    [Fact]
    public void Equal_WhenAValueIsSupplied_Throws()
    {
        Action act = () => ShareCalculator.Calculate(ExpenseId, 10.00m, SplitMethod.Equal, Weighted(1m, 1m));

        act.Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void Shares_UsesTheWeightsAndRemembersWhatWasTyped()
    {
        IReadOnlyList<ExpenseShare> shares =
            ShareCalculator.Calculate(ExpenseId, 100.00m, SplitMethod.Shares, Weighted(2m, 1m, 1m));

        AmountsOf(shares).Should().Equal(50.00m, 25.00m, 25.00m);
        shares.Select(share => share.InputWeight).Should().Equal(2m, 1m, 1m);
    }

    [Fact]
    public void Shares_WhenAWeightIsNotPositive_Throws()
    {
        Action act = () => ShareCalculator.Calculate(ExpenseId, 100.00m, SplitMethod.Shares, Weighted(2m, 0m));

        act.Should().ThrowExactly<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Percentages_SplitByPercent()
    {
        IReadOnlyList<ExpenseShare> shares = ShareCalculator.Calculate(
            ExpenseId, 100.00m, SplitMethod.Percentages, Weighted(33.33m, 33.33m, 33.34m));

        AmountsOf(shares).Should().Equal(33.33m, 33.33m, 33.34m);
    }

    private static readonly decimal[] JustUnderByACent = [33.33m, 33.33m, 33.33m];
    private static readonly decimal[] ShortByACent = [50.00m, 49.99m];
    private static readonly decimal[] OverByTen = [60.00m, 50.00m];

    public static TheoryData<decimal[]> PercentagesThatMiss() => new()
    {
        JustUnderByACent,
        ShortByACent,
        OverByTen,
    };

    [Theory]
    [MemberData(nameof(PercentagesThatMiss))]
    public void Percentages_WhenTheyDoNotAddUpToOneHundred_Throws(decimal[] percentages)
    {
        Action act = () =>
            ShareCalculator.Calculate(ExpenseId, 100.00m, SplitMethod.Percentages, Weighted(percentages));

        act.Should().ThrowExactly<ExpenseException>()
            .Which.Code.Should().Be(ExpenseException.PercentagesSumMismatch.Code);
    }

    [Fact]
    public void ExactAmounts_AreTakenAsTyped()
    {
        IReadOnlyList<ExpenseShare> shares = ShareCalculator.Calculate(
            ExpenseId, 100.00m, SplitMethod.ExactAmounts, Weighted(60.00m, 25.50m, 14.50m));

        AmountsOf(shares).Should().Equal(60.00m, 25.50m, 14.50m);
        shares.Select(share => share.InputWeight).Should().AllSatisfy(weight => weight.Should().BeNull());
    }

    [Fact]
    public void ExactAmounts_WhenTheyDoNotAddUpToTheTotal_Throws()
    {
        Action act = () =>
            ShareCalculator.Calculate(ExpenseId, 100.00m, SplitMethod.ExactAmounts, Weighted(60.00m, 39.99m));

        act.Should().ThrowExactly<ExpenseException>()
            .Which.Code.Should().Be(ExpenseException.SharesSumMismatch.Code);
    }

    [Theory]
    [InlineData(SplitMethod.Equal)]
    [InlineData(SplitMethod.Shares)]
    [InlineData(SplitMethod.Percentages)]
    [InlineData(SplitMethod.ExactAmounts)]
    public void Calculate_SharesAlwaysAddUpToTheAmount(SplitMethod method)
    {
        IReadOnlyList<ShareInput> inputs = method switch
        {
            SplitMethod.Equal => ForMembers(7),
            SplitMethod.Shares => Weighted(3m, 2m, 1m),
            SplitMethod.Percentages => Weighted(33.33m, 33.33m, 33.34m),
            _ => Weighted(3.34m, 3.33m, 3.33m),
        };
        decimal amount = method == SplitMethod.ExactAmounts ? 10.00m : 10.00m;

        IReadOnlyList<ExpenseShare> shares = ShareCalculator.Calculate(ExpenseId, amount, method, inputs);

        shares.Sum(share => share.ShareAmount).Should().Be(amount);
    }

    [Fact]
    public void Calculate_WhenAMemberAppearsTwice_Throws()
    {
        Guid duplicated = MemberIds.Ascending(1)[0];
        IReadOnlyList<ShareInput> inputs = [new(duplicated), new(duplicated)];

        Action act = () => ShareCalculator.Calculate(ExpenseId, 10.00m, SplitMethod.Equal, inputs);

        // A form that lets the same person be picked twice sends exactly this, so it is a
        // validation failure the API reports as 422 — not a programming error.
        act.Should().ThrowExactly<ExpenseException>()
            .Which.Code.Should().Be("validation_failed");
    }

    [Fact]
    public void Calculate_WhenThereAreNoMembers_Throws()
    {
        Action act = () => ShareCalculator.Calculate(ExpenseId, 10.00m, SplitMethod.Equal, []);

        act.Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void Calculate_ByItems_IsNotSupportedYet()
    {
        Action act = () => ShareCalculator.Calculate(ExpenseId, 10.00m, SplitMethod.ByItems, ForMembers(2));

        act.Should().ThrowExactly<NotSupportedException>();
    }

    [Fact]
    public void Calculate_UntilMultiCurrencyArrives_BaseAmountEqualsShareAmount()
    {
        IReadOnlyList<ExpenseShare> shares =
            ShareCalculator.Calculate(ExpenseId, 10.00m, SplitMethod.Equal, ForMembers(3));

        shares.Should().AllSatisfy(share =>
            share.ShareAmountInBaseCurrency.Should().Be(share.ShareAmount));
    }
}
