namespace Geteilt.Application.Users;

/// <summary>The four things a client can do with a session.</summary>
public interface IAuthenticationService
{
    Task<AuthenticationResult> RegisterAsync(
        string email,
        string displayName,
        string password,
        string? clientIp,
        CancellationToken cancellationToken);

    Task<AuthenticationResult> LoginAsync(
        string email,
        string password,
        string? clientIp,
        CancellationToken cancellationToken);

    Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        string? clientIp,
        CancellationToken cancellationToken);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
}
