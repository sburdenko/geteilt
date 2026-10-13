namespace Geteilt.Web.Auth;

/// <summary>
/// Renews the access token, once at a time, for the whole application.
/// </summary>
/// <remarks>
/// The gate lives here rather than in <see cref="AuthTokenHandler"/> on purpose. Every typed
/// client gets its own handler instance, so a gate held by the handler would let the groups client
/// and the expenses client renew in parallel. Refresh tokens rotate and the server revokes the
/// entire chain when a spent one comes back — two parallel renewals would sign the
/// person out rather than keep them in.
/// </remarks>
public sealed class SessionRenewer(AuthState auth, ITokenRefresher refresher, TimeProvider clock) : IDisposable
{
    /// <summary>
    /// How far before expiry to renew. Covers the round trip and a clock a few seconds off the
    /// server's; without it every token would be spent by one failed request first.
    /// </summary>
    private static readonly TimeSpan RenewAhead = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool DueForRenewal(AuthSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return clock.GetUtcNow() >= session.AccessTokenExpiresAt - RenewAhead;
    }

    public async Task<AuthSession?> RenewAsync(AuthSession stale, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stale);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            AuthSession? current = auth.Session;

            if (current is null)
            {
                return null;
            }

            // Somebody renewed while this call queued. The token we arrived with is already spent.
            if (!string.Equals(current.RefreshToken, stale.RefreshToken, StringComparison.Ordinal))
            {
                return current;
            }

            AuthSession? renewed = await refresher.RefreshAsync(stale.RefreshToken, cancellationToken);

            if (renewed is null)
            {
                await auth.ClearAsync();

                return null;
            }

            await auth.SetAsync(renewed);

            return renewed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
