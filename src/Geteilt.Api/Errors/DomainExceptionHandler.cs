using Geteilt.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Geteilt.Api.Errors;

/// <summary>
/// Turns a deliberate domain failure into ProblemDetails. These are expected outcomes, so they are
/// logged at warning level, not error — an alert on "user typed an invalid IBAN" is noise.
/// </summary>
internal sealed partial class DomainExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Domain rule {Code} rejected {Method} {Path}: {Reason}")]
    private static partial void LogRejection(ILogger logger, string code, string method, string path, string reason);


    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        int status = ErrorResponse.StatusFor(domainException.Kind);

        LogRejection(
            logger,
            domainException.Code,
            httpContext.Request.Method,
            httpContext.Request.Path,
            domainException.Message);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Type = ErrorResponse.TypeFor(domainException.Code),
                Title = domainException.Error.Title,
                Status = status,
                Detail = domainException.Message,
                Extensions = { ["code"] = domainException.Code },
            },
        });
    }
}
