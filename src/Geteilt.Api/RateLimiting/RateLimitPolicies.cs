using System.Threading.RateLimiting;
using Geteilt.Api.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Geteilt.Api.RateLimiting;

internal static class RateLimitPolicies
{
    internal const string Login = "login";

    private const int LoginAttemptsPerMinute = 5;

    internal static IServiceCollection AddGeteiltRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            // Per IP rather than per account: the point is to slow down someone working through a
            // list of addresses, and they get to choose which account they aim at.
            // Behind the proxy the address is the caller's, not the proxy's: the forwarded headers
            // are applied before this runs, and trusted from the proxy network only.
            options.AddPolicy(Login, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LoginAttemptsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Type = ErrorResponse.TypeFor("rate_limited"),
                        Title = "Too many requests",
                        Status = StatusCodes.Status429TooManyRequests,
                        Detail = "Too many attempts from this address. Try again shortly.",
                        Instance = context.HttpContext.Request.Path,
                        Extensions =
                        {
                            ["code"] = "rate_limited",
                            ["traceId"] = ErrorResponse.TraceIdOf(context.HttpContext),
                        },
                    },
                    cancellationToken);
            };
        });
}
