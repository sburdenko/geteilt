using Geteilt.Contracts.Auth;
using Geteilt.Contracts.Users;

namespace Geteilt.Web.Auth;

/// <summary>Everything a signed-in browser tab needs, and the whole of what gets persisted.</summary>
public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User)
{
    public static AuthSession From(AuthResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new AuthSession(
            response.AccessToken,
            response.AccessTokenExpiresAt,
            response.RefreshToken,
            response.RefreshTokenExpiresAt,
            response.User);
    }

    public bool RefreshTokenExpired(DateTimeOffset now) => now >= RefreshTokenExpiresAt;
}
