using Geteilt.Contracts.Auth;
using Geteilt.Web.Auth;

namespace Geteilt.Web.Api;

/// <summary>
/// The anonymous half of the API. Runs on its own <see cref="HttpClient"/>, without
/// <see cref="AuthTokenHandler"/>: these calls have no token to attach, and a refresh that went
/// through the handler would recurse on its own 401.
/// </summary>
public sealed class AuthApi(HttpClient http) : ITokenRefresher
{
    public Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<AuthResponse>(http, Anonymous("api/auth/login", request), cancellationToken);

    public Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<AuthResponse>(http, Anonymous("api/auth/register", request), cancellationToken);

    public async Task<AuthSession?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        Result<AuthResponse> result = await ApiCall.SendAsync<AuthResponse>(
            http, Anonymous("api/auth/refresh", new RefreshRequest(refreshToken)), cancellationToken);

        return result.Match<AuthSession?>(AuthSession.From, _ => null);
    }

    /// <summary>
    /// Revokes the refresh token server-side. The local session is cleared either way — a person
    /// who pressed sign-out stays signed out even if the network was down when they did.
    /// </summary>
    public Task<Result<Unit>> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync(http, Anonymous("api/auth/logout", new RefreshRequest(refreshToken)), cancellationToken);

    private static HttpRequestMessage Anonymous<TBody>(string path, TBody body)
    {
        HttpRequestMessage request = ApiCall.Post(path, body);

        // The endpoints carry [SkipIdempotency], so the key would be read by nothing.
        request.Headers.Remove("Idempotency-Key");

        return request;
    }
}
