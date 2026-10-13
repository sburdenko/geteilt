using Geteilt.Application.Users;

namespace Geteilt.Application.Ports;

/// <summary>
/// Everything the application needs from the authentication provider. Declared here so no layer
/// above Infrastructure ever sees ASP.NET Core Identity.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Creates the authentication record and the domain user together, or neither of them.
    /// </summary>
    Task<RegistrationResult> RegisterAsync(
        string email,
        string displayName,
        string password,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks a password and counts the attempt towards lockout. A wrong password and an unknown
    /// address give the same answer on purpose: telling them apart turns the login form into a
    /// way to find out who has an account here.
    /// </summary>
    Task<CredentialCheck> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    /// <summary>Who a token belongs to, for minting a fresh one during refresh.</summary>
    Task<AuthenticatedUser?> FindAsync(Guid userId, CancellationToken cancellationToken);
}
