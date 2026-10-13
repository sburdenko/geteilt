namespace Geteilt.Domain.Tests;

public sealed class MoneyAllocateValidationTests
{
    // decimal is not a legal attribute argument type, so these tables cannot use [InlineData]:
    // it would smuggle a double into the one place where double is banned outright.
    public static TheoryData<decimal> NonPositiveTotals() => new() { 0m, -0.01m, -100.00m };

    public static TheoryData<decimal> FractionalCentTotals() => new() { 10.005m, 0.001m, 1.9999m };

    [Theory]
    [MemberData(nameof(NonPositiveTotals))]
    public void Allocate_WhenTotalIsNotPositive_Throws(decimal total)
    {
        Action act = () => Money.Allocate(total, MemberIds.EqualWeights(2), MemberIds.Ascending(2));

        act.Should().ThrowExactly<ArgumentOutOfRangeException>().WithParameterName(nameof(total));
    }

    [Theory]
    [MemberData(nameof(FractionalCentTotals))]
    public void Allocate_WhenTotalIsNotAWholeNumberOfCents_Throws(decimal total)
    {
        Action act = () => Money.Allocate(total, MemberIds.EqualWeights(2), MemberIds.Ascending(2));

        act.Should().ThrowExactly<ArgumentOutOfRangeException>().WithParameterName(nameof(total));
    }

    [Fact]
    public void Allocate_WhenWeightsAreEmpty_Throws()
    {
        Action act = () => Money.Allocate(10.00m, [], []);

        act.Should().ThrowExactly<ArgumentException>().WithParameterName("weights");
    }

    [Fact]
    public void Allocate_WhenAWeightIsNegative_Throws()
    {
        Action act = () => Money.Allocate(10.00m, [1m, -1m], MemberIds.Ascending(2));

        act.Should().ThrowExactly<ArgumentOutOfRangeException>().WithParameterName("weights");
    }

    [Fact]
    public void Allocate_WhenAllWeightsAreZero_Throws()
    {
        Action act = () => Money.Allocate(10.00m, [0m, 0m], MemberIds.Ascending(2));

        act.Should().ThrowExactly<ArgumentException>().WithParameterName("weights");
    }

    [Fact]
    public void Allocate_WhenMemberCountDoesNotMatchWeightCount_Throws()
    {
        Action act = () => Money.Allocate(10.00m, [1m, 1m], MemberIds.Ascending(3));

        act.Should().ThrowExactly<ArgumentException>().WithParameterName("memberIds");
    }

    [Fact]
    public void Allocate_WhenTheSameMemberAppearsTwice_Throws()
    {
        Guid duplicated = new("00000000-0000-0000-0000-000000000001");

        Action act = () => Money.Allocate(10.00m, [1m, 1m], [duplicated, duplicated]);

        act.Should().ThrowExactly<ArgumentException>().WithParameterName("memberIds");
    }

    [Fact]
    public void Allocate_WhenWeightsAreNull_Throws()
    {
        Action act = () => Money.Allocate(10.00m, null!, MemberIds.Ascending(2));

        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("weights");
    }

    [Fact]
    public void Allocate_WhenMemberIdsAreNull_Throws()
    {
        Action act = () => Money.Allocate(10.00m, MemberIds.EqualWeights(2), null!);

        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("memberIds");
    }
}
