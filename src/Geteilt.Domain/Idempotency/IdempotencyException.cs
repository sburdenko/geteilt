namespace Geteilt.Domain.Idempotency;

/// <summary>The Idempotency-Key header is missing or being reused wrongly.</summary>
public sealed class IdempotencyException : DomainException
{
    public static readonly DomainError KeyRequired =
        new("idempotency_key_required", DomainErrorKind.Malformed, "This request needs an Idempotency-Key header");

    public static readonly DomainError KeyReuse =
        new("idempotency_key_reuse", DomainErrorKind.Conflict, "That Idempotency-Key was used for a different request");

    private IdempotencyException(DomainError error, string message)
        : base(error, message)
    {
    }

    public static IdempotencyException Missing() => new(
        KeyRequired,
        "POST requests that change state must carry an Idempotency-Key header, a UUID chosen by the client.");

    /// <summary>
    /// A conflict rather than a validation failure: the key is fine, it just already belongs to a
    /// different request. Reusing it would make one of the two bodies silently disappear.
    /// </summary>
    public static IdempotencyException Reused(string key) => new(
        KeyReuse,
        $"Idempotency-Key '{key}' was already used for a request with a different body.");
}
