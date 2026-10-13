using Geteilt.Web.Api;

namespace Geteilt.Web.Tests;

/// <summary>Two calls a screen needs together: it has both or it has an error.</summary>
public sealed class ResultZipTests
{
    private static readonly ApiError First = ApiError.Unreachable("first");
    private static readonly ApiError Second = ApiError.Unreachable("second");

    [Fact]
    public void TwoSuccesses_AreCombined()
    {
        Result<string> zipped = new Result<int>.Ok(2).Zip(new Result<string>.Ok("x"), (n, s) => $"{n}{s}");

        zipped.Should().Be(new Result<string>.Ok("2x"));
    }

    [Fact]
    public void AFailureOnEitherSide_IsTheResult()
    {
        new Result<int>.Failed(First).Zip(new Result<int>.Ok(1), (a, b) => a + b)
            .Should().Be(new Result<int>.Failed(First));

        new Result<int>.Ok(1).Zip(new Result<int>.Failed(Second), (a, b) => a + b)
            .Should().Be(new Result<int>.Failed(Second));
    }

    [Fact]
    public void TwoFailures_ReportTheFirst()
    {
        new Result<int>.Failed(First).Zip(new Result<int>.Failed(Second), (a, b) => a + b)
            .Should().Be(new Result<int>.Failed(First));
    }
}
