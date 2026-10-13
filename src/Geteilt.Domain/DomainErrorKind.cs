namespace Geteilt.Domain;

/// <summary>
/// What sort of failure this is, in domain terms. The API turns it into an HTTP status code —
/// the domain deliberately knows nothing about HTTP, so it says what happened, not how to report it.
/// </summary>
public enum DomainErrorKind
{
    /// <summary>The input breaks a rule. No state of the system would have made it acceptable.</summary>
    Validation,

    /// <summary>
    /// The request is not well formed — a required header is absent, say. Distinct from
    /// <see cref="Validation"/>, which is about a value the caller did send: there is nothing
    /// here to find fault with yet, so the API answers 400 rather than 422.
    /// </summary>
    Malformed,

    /// <summary>The request contradicts the current state — it might have succeeded a minute ago.</summary>
    Conflict,

    /// <summary>The thing addressed does not exist, or must not be revealed to exist.</summary>
    NotFound,

    /// <summary>The caller is known, but not allowed to do this.</summary>
    Forbidden,
}
