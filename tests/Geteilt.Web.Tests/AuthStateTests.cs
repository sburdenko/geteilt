using Geteilt.Web.Auth;
using Microsoft.Extensions.Time.Testing;

namespace Geteilt.Web.Tests;

/// <summary>Reloading the page must not drop the person at the login screen.</summary>
public sealed class AuthStateTests
{
    private readonly InMemoryTokenStorage _storage = new();
    private readonly FakeTimeProvider _clock = new(Sessions.Now);

    [Fact]
    public async Task AStoredSession_ComesBackAfterAReload()
    {
        _storage.Stored = Sessions.At("one", TimeSpan.FromMinutes(10));
        AuthState auth = new(_storage);

        await auth.RestoreAsync(_clock);

        auth.SignedIn.Should().BeTrue();
        auth.User!.DisplayName.Should().Be("Anna");
    }

    [Fact]
    public async Task ASessionWhoseRefreshTokenDied_IsDiscardedInsteadOfRestored()
    {
        _storage.Stored = Sessions.At("one", TimeSpan.FromMinutes(10));
        AuthState auth = new(_storage);
        _clock.SetUtcNow(Sessions.Now + TimeSpan.FromDays(31));

        await auth.RestoreAsync(_clock);

        // Restoring it would show the app for one request and then bounce to login. Better to
        // start at login.
        auth.SignedIn.Should().BeFalse();
        _storage.Clears.Should().Be(1);
    }

    [Fact]
    public async Task SigningOut_LeavesNothingBehindInStorage()
    {
        AuthState auth = new(_storage);
        await auth.SetAsync(Sessions.At("one", TimeSpan.FromMinutes(10)));

        await auth.ClearAsync();

        auth.Session.Should().BeNull();
        _storage.Stored.Should().BeNull();
    }

    [Fact]
    public async Task EveryChange_TellsTheUiToRedraw()
    {
        AuthState auth = new(_storage);
        int notifications = 0;
        auth.Changed += () => notifications++;

        await auth.SetAsync(Sessions.At("one", TimeSpan.FromMinutes(10)));
        await auth.ClearAsync();

        notifications.Should().Be(2);
    }
}
