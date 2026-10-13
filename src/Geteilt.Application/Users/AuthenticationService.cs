using Geteilt.Application.Ports;
using Geteilt.Contracts.Auth;
using Geteilt.Contracts.Users;

namespace Geteilt.Application.Users;

/// <summary>
/// Puts the three halves of a session together: who you are, a short-lived access token, and a
/// long-lived refresh token. Each of those is a port; this is the use case that combines them.
/// </summary>
public sealed class AuthenticationService(
    IIdentityService identity,
    IAccessTokenService accessTokens,
    IRefreshTokenService refreshTokens) : IAuthenticationService
{
    public async Task<AuthenticationResult> RegisterAsync(
        string email,
        string displayName,
        string password,
        string? clientIp,
        CancellationToken cancellationToken)
    {
        RegistrationResult registration =
            await identity.RegisterAsync(email, displayName, password, cancellationToken);

        if (!registration.Succeeded)
        {
            return AuthenticationResult.Failed(
                registration.Outcome == RegistrationOutcome.EmailAlreadyRegistered
                    ? AuthenticationOutcome.EmailAlreadyRegistered
                    : AuthenticationOutcome.PasswordRejected,
                registration.Errors);
        }

        AuthenticatedUser? user = await identity.FindAsync(registration.UserId, cancellationToken);

        return user is null
            ? AuthenticationResult.Failed(AuthenticationOutcome.InvalidCredentials)
            : AuthenticationResult.Success(await IssueAsync(user, clientIp, cancellationToken));
    }

    public async Task<AuthenticationResult> LoginAsync(
        string email,
        string password,
        string? clientIp,
        CancellationToken cancellationToken)
    {
        CredentialCheck check = await identity.ValidateCredentialsAsync(email, password, cancellationToken);

        return check switch
        {
            { Outcome: CredentialOutcome.Success, User: { } user } =>
                AuthenticationResult.Success(await IssueAsync(user, clientIp, cancellationToken)),
            { Outcome: CredentialOutcome.LockedOut } =>
                AuthenticationResult.Failed(AuthenticationOutcome.LockedOut),
            _ => AuthenticationResult.Failed(AuthenticationOutcome.InvalidCredentials),
        };
    }

    public async Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        string? clientIp,
        CancellationToken cancellationToken)
    {
        RefreshResult redeemed = await refreshTokens.RedeemAsync(refreshToken, clientIp, cancellationToken);

        if (!redeemed.Succeeded)
        {
            // Unknown, expired and already-spent are one answer to the caller. Only the log
            // distinguishes them; a client that learns "this token was reused" learns nothing
            // useful and an attacker learns their copy was noticed.
            return AuthenticationResult.Failed(AuthenticationOutcome.RefreshRejected);
        }

        AuthenticatedUser? user = await identity.FindAsync(redeemed.UserId, cancellationToken);

        if (user is null)
        {
            return AuthenticationResult.Failed(AuthenticationOutcome.RefreshRejected);
        }

        // The refresh token was already rotated by RedeemAsync; only the access token is new here.
        AccessToken access = accessTokens.Issue(user.Id, user.Email, user.DisplayName);

        return AuthenticationResult.Success(Respond(user, access, redeemed.Token, redeemed.ExpiresAt));
    }

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken) =>
        refreshTokens.RevokeAsync(refreshToken, cancellationToken);

    private async Task<AuthResponse> IssueAsync(
        AuthenticatedUser user,
        string? clientIp,
        CancellationToken cancellationToken)
    {
        AccessToken access = accessTokens.Issue(user.Id, user.Email, user.DisplayName);
        IssuedRefreshToken refresh = await refreshTokens.IssueAsync(user.Id, clientIp, cancellationToken);

        return Respond(user, access, refresh.Value, refresh.ExpiresAt);
    }

    private static AuthResponse Respond(
        AuthenticatedUser user,
        AccessToken access,
        string refreshToken,
        DateTimeOffset refreshExpiresAt) =>
        new(
            access.Value,
            access.ExpiresAt,
            refreshToken,
            refreshExpiresAt,
            new UserDto(user.Id, user.Email, user.DisplayName, user.Iban, user.PreferredCurrency));
}
