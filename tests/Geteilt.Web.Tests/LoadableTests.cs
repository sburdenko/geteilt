using Geteilt.Web.Api;
using Geteilt.Web.Loading;

namespace Geteilt.Web.Tests;

/// <summary>What a screen shows while its data is on the way.</summary>
public sealed class LoadableTests
{
    private static readonly ApiError Offline = ApiError.Unreachable("connection refused");

    [Fact]
    public void ASuccessfulCall_IsLoadedWithItsValue()
    {
        Loadable<int> state = new Result<int>.Ok(42).ToLoadable();

        state.Should().Be(new Loadable<int>.Loaded(42));
    }

    [Fact]
    public void AFailedCall_IsFailedWithItsError()
    {
        Loadable<int> state = new Result<int>.Failed(Offline).ToLoadable();

        state.Should().Be(new Loadable<int>.Failed(Offline));
    }

    [Fact]
    public void Map_ChangesALoadedValue()
    {
        Loadable<int> state = new Loadable<int>.Loaded(1).Map(value => value + 1);

        state.Should().Be(new Loadable<int>.Loaded(2));
    }

    [Fact]
    public void Map_LeavesLoadingAndFailedAlone()
    {
        // An edit that arrives before the data, or after it failed, has nothing to apply to.
        new Loadable<int>.Loading().Map(value => value + 1).Should().Be(new Loadable<int>.Loading());
        new Loadable<int>.Failed(Offline).Map(value => value + 1).Should().Be(new Loadable<int>.Failed(Offline));
    }

    [Fact]
    public void Match_AnswersForEachStateSeparately()
    {
        static string Describe(Loadable<int> state) =>
            state.Match(() => "loading", value => $"loaded {value}", error => $"failed {error.Code}");

        Describe(new Loadable<int>.Loading()).Should().Be("loading");
        Describe(new Loadable<int>.Loaded(7)).Should().Be("loaded 7");
        Describe(new Loadable<int>.Failed(Offline)).Should().Be($"failed {ApiError.UnreachableCode}");
    }
}
