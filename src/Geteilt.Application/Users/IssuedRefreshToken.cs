namespace Geteilt.Application.Users;

/// <param name="Value">The token itself. This is the only moment it exists in clear text — the database keeps a hash.</param>
public sealed record IssuedRefreshToken(string Value, DateTimeOffset ExpiresAt);
