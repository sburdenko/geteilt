namespace Geteilt.Web.Auth;

/// <summary>
/// Trades a refresh token for a new session. Deliberately not part of the typed API client: it
/// must go out on an <c>HttpClient</c> without <see cref="AuthTokenHandler"/>, or a refresh that
/// answers 401 would try to refresh itself.
/// </summary>
public interface ITokenRefresher
{
    Task<AuthSession?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
