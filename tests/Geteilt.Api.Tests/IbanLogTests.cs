using Geteilt.Api.Logging;

namespace Geteilt.Api.Tests;

/// <summary>
/// A full IBAN must never reach the logs. This pins the rule itself; the single
/// place that logs one calls straight into it.
/// </summary>
public sealed class IbanLogTests
{
    [Fact]
    public void Tail_KeepsOnlyTheLastFourCharacters()
    {
        IbanLog.Tail("DE89370400440532013000").Should().Be("3000");
    }

    [Fact]
    public void Tail_RevealsNothingElseOfTheAccount()
    {
        const string iban = "DE89370400440532013000";

        string tail = IbanLog.Tail(iban);

        tail.Should().HaveLength(IbanLog.VisibleCharacters);
        iban.Should().NotBe(tail);
        iban[..^IbanLog.VisibleCharacters].Should().NotContain(tail);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("AB", "**")]
    [InlineData("ABCD", "****")]
    public void Tail_HidesAnythingTooShortToTruncate(string input, string expected)
    {
        // Four characters of a four-character string would be all of it.
        IbanLog.Tail(input).Should().Be(expected);
    }

    [Fact]
    public void Tail_OfNothing_IsEmpty()
    {
        IbanLog.Tail(null).Should().BeEmpty();
    }
}
