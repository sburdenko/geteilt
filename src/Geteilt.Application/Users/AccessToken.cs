namespace Geteilt.Application.Users;

/// <param name="Value">The signed token itself.</param>
/// <param name="ExpiresAt">When it stops being accepted, so a client can refresh before it does.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
