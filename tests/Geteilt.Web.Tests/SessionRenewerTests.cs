using Geteilt.Web.Auth;
using Microsoft.Extensions.Time.Testing;

namespace Geteilt.Web.Tests;

/// <summary>
/// An expired access token is renewed without the person
/// noticing, and the failure mode that would break it worst.
/// </summary>
public sealed class SessionRenewerTests
{
    private readonly InMemoryTokenStorage _storage = new();
    private readonly FakeTimeProvider _clock = new(Sessions.Now);

    [Fact]
    public async Task TwoCallsRacingToRenew_SpendTheRefreshTokenOnce()
    {
        AuthSession stale = Sessions.At("one", TimeSpan.Zero);
        TaskCompletionSource gate = new();
        CountingRefresher refresher = new(_ => Sessions.At("two", TimeSpan.FromMinutes(15))) { Gate = gate };
        (AuthState auth, SessionRenewer renewer) = Build(refresher);
        await auth.SetAsync(stale);

        Task<AuthSession?> first = renewer.RenewAsync(stale, CancellationToken.None);
        Task<AuthSession?> second = renewer.RenewAsync(stale, CancellationToken.None);
        gate.SetResult();

        AuthSession?[] renewed = await Task.WhenAll(first, second);

        // Refresh tokens rotate and the server revokes the whole chain when a spent one comes back
        //. A second exchange here would sign the person out mid-session — the exact
        // opposite of what transparent refresh is for.
        refresher.Spent.Should().ContainSingle().Which.Should().Be("refresh-one");
        renewed.Should().AllSatisfy(session => session!.AccessToken.Should().Be("access-two"));
    }

    [Fact]
    public async Task ACallerThatArrivesAfterSomebodyElseRenewed_GetsTheNewSessionWithoutSpendingAnything()
    {
        AuthSession stale = Sessions.At("one", TimeSpan.Zero);
        AuthSession fresh = Sessions.At("two", TimeSpan.FromMinutes(15));
        CountingRefresher refresher = new(_ => throw new InvalidOperationException("must not be called"));
        (AuthState auth, SessionRenewer renewer) = Build(refresher);
        await auth.SetAsync(fresh);

        AuthSession? result = await renewer.RenewAsync(stale, CancellationToken.None);

        result.Should().Be(fresh);
        refresher.Spent.Should().BeEmpty();
    }

    [Fact]
    public async Task ARefreshTheServerRejects_ClearsTheSession()
    {
        AuthSession stale = Sessions.At("one", TimeSpan.Zero);
        (AuthState auth, SessionRenewer renewer) = Build(new CountingRefresher(_ => null));
        await auth.SetAsync(stale);

        AuthSession? result = await renewer.RenewAsync(stale, CancellationToken.None);

        result.Should().BeNull();
        auth.SignedIn.Should().BeFalse();
        _storage.Stored.Should().BeNull();
    }

    [Theory]
    [InlineData(31, false)]
    [InlineData(29, true)]
    [InlineData(-1, true)]
    public void RenewalIsDue_AShortWhileBeforeExpiryRatherThanAfterIt(int secondsLeft, bool expected)
    {
        (_, SessionRenewer renewer) = Build(new CountingRefresher(_ => null));

        // Renewing only once the token is already dead costs one failed request every time.
        renewer.DueForRenewal(Sessions.At("one", TimeSpan.FromSeconds(secondsLeft)))
            .Should().Be(expected);
    }

    private (AuthState Auth, SessionRenewer Renewer) Build(ITokenRefresher refresher)
    {
        AuthState auth = new(_storage);

        return (auth, new SessionRenewer(auth, refresher, _clock));
    }
}
