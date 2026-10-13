using Geteilt.Contracts.Users;

namespace Geteilt.Web.Auth;

/// <summary>Who is signed in, for the whole app.</summary>
public sealed class AuthState(ITokenStorage storage)
{
    public AuthSession? Session { get; private set; }

    public UserDto? User => Session?.User;

    public bool SignedIn => Session is not null;

    public event Action? Changed;

    /// <summary>
    /// Called once before the first render, so a reload lands on the page the person was on
    /// instead of the login screen.
    /// </summary>
    public async Task RestoreAsync(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        AuthSession? stored = await storage.ReadAsync();

        if (stored is null)
        {
            return;
        }

        if (stored.RefreshTokenExpired(clock.GetUtcNow()))
        {
            await storage.ClearAsync();

            return;
        }

        Session = stored;
        Changed?.Invoke();
    }

    public async Task SetAsync(AuthSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
        await storage.WriteAsync(session);
        Changed?.Invoke();
    }

    /// <summary>Forgets the session here. Revoking it on the server is the caller's job.</summary>
    public async Task ClearAsync()
    {
        Session = null;
        await storage.ClearAsync();
        Changed?.Invoke();
    }
}
