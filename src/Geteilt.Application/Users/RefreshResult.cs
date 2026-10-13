namespace Geteilt.Application.Users;

/// <summary>Outcome of redeeming a refresh token.</summary>
public sealed record RefreshResult
{
    private RefreshResult(RefreshOutcome outcome, Guid userId, string token, DateTimeOffset expiresAt)
    {
        Outcome = outcome;
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
    }

    public RefreshOutcome Outcome { get; }

    public Guid UserId { get; }

    /// <summary>The successor token. Empty unless the redemption succeeded.</summary>
    public string Token { get; }

    public DateTimeOffset ExpiresAt { get; }

    public bool Succeeded => Outcome == RefreshOutcome.Success;

    public static RefreshResult Success(Guid userId, string token, DateTimeOffset expiresAt) =>
        new(RefreshOutcome.Success, userId, token, expiresAt);

    public static RefreshResult Failed(RefreshOutcome outcome) =>
        new(outcome, Guid.Empty, string.Empty, default);
}
