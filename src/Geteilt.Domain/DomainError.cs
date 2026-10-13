namespace Geteilt.Domain;

/// <summary>
/// Identity of a domain failure: what it is called, how severe it is, and how to describe it to
/// a human. Fixed per kind of problem — RFC 9457 asks that <c>title</c> not vary between
/// occurrences, while the per-occurrence part goes in the exception message.
/// </summary>
/// <param name="Code">Machine-readable and stable. Part of the API contract.</param>
/// <param name="Kind">What sort of failure this is; the API turns it into a status code.</param>
/// <param name="Title">Short human summary, the same for every occurrence.</param>
public sealed record DomainError(string Code, DomainErrorKind Kind, string Title);
