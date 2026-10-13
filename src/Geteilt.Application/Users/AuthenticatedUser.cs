namespace Geteilt.Application.Users;

/// <summary>
/// The person behind a session. Id, email and name go into the access token; the rest is what the
/// client shows without asking again.
/// </summary>
public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string DisplayName,
    string? Iban,
    string PreferredCurrency);
