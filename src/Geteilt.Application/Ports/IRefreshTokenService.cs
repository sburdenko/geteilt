using Geteilt.Application.Users;

namespace Geteilt.Application.Ports;

/// <summary>
/// Long-lived tokens that buy a new access token without asking for the password again.
/// Every redemption rotates: the token used is spent and a successor takes its place.
/// </summary>
public interface IRefreshTokenService
{
    Task<IssuedRefreshToken> IssueAsync(Guid userId, string? clientIp, CancellationToken cancellationToken);

    /// <summary>
    /// Spends a token and returns its successor. A token that was already spent means somebody
    /// holds a copy, and the whole chain dies.
    /// </summary>
    Task<RefreshResult> RedeemAsync(string token, string? clientIp, CancellationToken cancellationToken);

    /// <summary>Ends one session — what logout does. Unknown tokens are ignored rather than reported.</summary>
    Task RevokeAsync(string token, CancellationToken cancellationToken);
}
