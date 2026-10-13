namespace Geteilt.Infrastructure.Identity;

/// <summary>
/// One link in a chain of refresh tokens. Redeeming a token revokes it and issues a successor,
/// so a chain records the whole history of one login session.
/// </summary>
/// <remarks>
/// Lives beside <see cref="AppUser"/> in Infrastructure rather than in the domain for the same
/// reason: a refresh token is not part of the language of splitting expenses. The domain is
/// compiled into the mobile client to compute balances offline, and session bookkeeping has no
/// business travelling with it.
/// </remarks>
public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string? createdByIp)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>
    /// The hash, never the token. A leaked database must not hand out working sessions.
    /// </summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>The successor handed out when this token was redeemed; the link that forms the chain.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>Kept for forensics after a theft, not for authorisation.</summary>
    public string? CreatedByIp { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset now, Guid? replacedByTokenId = null)
    {
        // First revocation wins: re-revoking would overwrite the moment a theft was detected.
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        ReplacedByTokenId = replacedByTokenId;
    }
}
