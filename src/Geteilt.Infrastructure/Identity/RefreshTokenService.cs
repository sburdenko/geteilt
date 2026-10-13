using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Geteilt.Infrastructure.Identity;

public sealed partial class RefreshTokenService(
    AppDbContext dbContext,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider,
    ILogger<RefreshTokenService> logger) : IRefreshTokenService
{
    /// <summary>256 bits from a cryptographic generator — enough that guessing is not a strategy.</summary>
    private const int TokenBytes = 32;

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Refresh token {TokenId} was redeemed twice; revoking the chain for user {UserId}")]
    private static partial void LogReuse(ILogger logger, Guid tokenId, Guid userId);

    public async Task<IssuedRefreshToken> IssueAsync(
        Guid userId,
        string? clientIp,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        (string token, string hash) = CreateToken();
        DateTimeOffset expiresAt = now.AddDays(options.Value.RefreshTokenDays);

        dbContext.RefreshTokens.Add(new RefreshToken(Guid.CreateVersion7(), userId, hash, now, expiresAt, clientIp));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedRefreshToken(token, expiresAt);
    }

    public async Task<RefreshResult> RedeemAsync(
        string token,
        string? clientIp,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        DateTimeOffset now = timeProvider.GetUtcNow();
        string hash = Hash(token);

        RefreshToken? stored = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return RefreshResult.Failed(RefreshOutcome.NotFound);
        }

        if (stored.RevokedAt is not null)
        {
            // The legitimate holder moved on to the successor long ago, so whoever sent this had a
            // copy. Which of the two is the thief is unknowable, so both lose the session.
            LogReuse(logger, stored.Id, stored.UserId);
            await RevokeChainFromAsync(stored, now, cancellationToken);

            return RefreshResult.Failed(RefreshOutcome.Reused);
        }

        if (now >= stored.ExpiresAt)
        {
            return RefreshResult.Failed(RefreshOutcome.Expired);
        }

        (string successorToken, string successorHash) = CreateToken();
        RefreshToken successor = new(
            Guid.CreateVersion7(),
            stored.UserId,
            successorHash,
            now,
            now.AddDays(options.Value.RefreshTokenDays),
            clientIp);

        dbContext.RefreshTokens.Add(successor);
        stored.Revoke(now, successor.Id);

        await dbContext.SaveChangesAsync(cancellationToken);

        return RefreshResult.Success(stored.UserId, successorToken, successor.ExpiresAt);
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        string hash = Hash(token);

        RefreshToken? stored = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return;
        }

        stored.Revoke(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Revokes every successor of a reused token, so no descendant survives the theft.</summary>
    private async Task RevokeChainFromAsync(
        RefreshToken reused,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // One query for the user's tokens, then the links are walked in memory: a chain is a
        // handful of rows, and following it in SQL would mean a round trip per link.
        Dictionary<Guid, RefreshToken> byId = await dbContext.RefreshTokens
            .Where(candidate => candidate.UserId == reused.UserId)
            .ToDictionaryAsync(candidate => candidate.Id, cancellationToken);

        Guid? next = reused.ReplacedByTokenId;

        while (next is { } id && byId.TryGetValue(id, out RefreshToken? link))
        {
            link.Revoke(now);
            next = link.ReplacedByTokenId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static (string Token, string Hash) CreateToken()
    {
        string token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

        return (token, Hash(token));
    }

    /// <summary>
    /// Plain SHA-256, not a password hash. A password is short and guessable, so it needs a slow
    /// KDF; this token is 256 random bits, and there is nothing to brute-force. Making the hash
    /// slow would only tax every refresh a client ever makes.
    /// </summary>
    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
