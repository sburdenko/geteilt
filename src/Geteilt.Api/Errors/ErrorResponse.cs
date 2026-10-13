using System.Diagnostics;
using Geteilt.Domain;
using Microsoft.AspNetCore.Http;

namespace Geteilt.Api.Errors;

/// <summary>Shared shape of every error the API returns (RFC 9457).</summary>
internal static class ErrorResponse
{
    internal const string TypeBaseUri = "https://geteilt.app/errors/";

    /// <summary>
    /// The same id Serilog writes into every log event as <c>@tr</c>, so a user's screenshot of an
    /// error leads straight to the request in the logs.
    /// </summary>
    internal static string TraceIdOf(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

    internal static string TypeFor(string code) =>
        TypeBaseUri + code.Replace('_', '-');

    internal static int StatusFor(DomainErrorKind kind) => kind switch
    {
        DomainErrorKind.Validation => StatusCodes.Status422UnprocessableEntity,
        DomainErrorKind.Malformed => StatusCodes.Status400BadRequest,
        DomainErrorKind.Conflict => StatusCodes.Status409Conflict,
        DomainErrorKind.NotFound => StatusCodes.Status404NotFound,
        DomainErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}
