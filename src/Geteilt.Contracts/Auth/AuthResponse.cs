using Geteilt.Contracts.Users;

namespace Geteilt.Contracts.Auth;

/// <summary>
/// Both tokens plus who they belong to, so a client can render the signed-in state without a
/// second round trip. Expiry times are returned so the client can refresh before the access token
/// dies rather than after a request has already failed.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);
