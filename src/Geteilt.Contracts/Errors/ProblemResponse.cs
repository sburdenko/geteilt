namespace Geteilt.Contracts.Errors;

/// <summary>
/// The error body every endpoint returns (RFC 9457), as a client reads it.
/// The server builds these through MVC's own <c>ProblemDetails</c>; this is the mirror image,
/// and it lives in Contracts because the web and the mobile client both have to parse it.
/// </summary>
public sealed record ProblemResponse(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Code,
    IReadOnlyDictionary<string, string[]>? Errors);
