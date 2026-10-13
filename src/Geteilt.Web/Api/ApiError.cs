using Geteilt.Contracts.Errors;

namespace Geteilt.Web.Api;

/// <summary>Why a call did not produce a value, in the form the UI needs to say so.</summary>
public sealed record ApiError(
    int Status,
    string Code,
    string Title,
    string? Detail,
    IReadOnlyDictionary<string, string[]> FieldErrors)
{
    /// <summary>The request never reached the API, or the answer was not a problem document.</summary>
    public const string UnreachableCode = "network_unreachable";

    public const string UnreadableCode = "response_unreadable";

    public static ApiError Unreachable(string detail) => new(
        0, UnreachableCode, "Keine Verbindung zum Server", detail, EmptyFields);

    public static ApiError Unreadable(int status, string? detail) => new(
        status, UnreadableCode, "Unerwartete Antwort vom Server", detail, EmptyFields);

    public static ApiError From(int status, ProblemResponse problem) => new(
        problem.Status ?? status,
        problem.Code ?? "unknown",
        // The toast shows `title`; a problem document without one would leave it blank.
        string.IsNullOrWhiteSpace(problem.Title) ? "Fehler" : problem.Title,
        problem.Detail,
        problem.Errors ?? EmptyFields);

    private static readonly IReadOnlyDictionary<string, string[]> EmptyFields =
        new Dictionary<string, string[]>();
}
