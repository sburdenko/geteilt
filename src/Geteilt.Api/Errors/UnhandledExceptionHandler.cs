using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Geteilt.Api.Errors;

/// <summary>
/// Last resort for anything the domain did not raise on purpose — that is, a bug.
/// </summary>
internal sealed partial class UnhandledExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    internal const string Code = "internal_error";

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // The exception is deliberately not attached to the context: doing so makes the framework
        // include the type and stack trace in Development, which means the shape of a 500 differs
        // between environments and the one that matters is the one nobody ever looks at.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Type = ErrorResponse.TypeFor(Code),
                Title = "Unexpected error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "The request could not be completed. Quote the traceId when reporting this.",
                Extensions = { ["code"] = Code },
            },
        });
    }
}
