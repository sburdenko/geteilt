namespace Geteilt.Domain.Idempotency;

/// <summary>
/// What one POST did, kept so a repeat of it can be answered without doing it twice.
/// </summary>
/// <remarks>
/// This exists from the start rather than "when there is load", because it is not a scale problem. A phone
/// on the underground sends an expense, loses the connection before the answer arrives and retries.
/// Without this the group gets a duplicate expense that nobody notices until the settling up a week
/// later.
/// </remarks>
public sealed class IdempotencyRecord
{
    public const int MaxKeyLength = 64;

    private IdempotencyRecord()
    {
    }

    public IdempotencyRecord(
        string key,
        Guid userId,
        string endpoint,
        string requestHash,
        int responseStatusCode,
        string? responseBodyJson,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(key.Length, MaxKeyLength);

        Key = key;
        UserId = userId;
        Endpoint = endpoint;
        RequestHash = requestHash;
        ResponseStatusCode = responseStatusCode;
        ResponseBodyJson = responseBodyJson;
        CreatedAt = createdAt;
    }

    /// <summary>Chosen by the client, usually a UUID.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>
    /// Whose key it is. Keys are scoped per person, so one client cannot collide with — or
    /// deliberately occupy — another's.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>Method and path, so the same key on a different endpoint is a different request.</summary>
    public string Endpoint { get; private set; } = string.Empty;

    /// <summary>SHA-256 of the raw request bytes.</summary>
    public string RequestHash { get; private set; } = string.Empty;

    public int ResponseStatusCode { get; private set; }

    public string? ResponseBodyJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
