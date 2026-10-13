using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Primitives;

namespace Geteilt.Api.Tests;

/// <summary>
/// Lets a test pretend to come from a particular address.
/// </summary>
/// <remarks>
/// TestServer leaves <c>RemoteIpAddress</c> null, so every request would share one rate-limit
/// partition and the login limit would leak between tests. This lives in the test project rather
/// than in the API: production must never take a caller's word for where it is calling from.
/// </remarks>
internal sealed class TestClientIpStartupFilter : IStartupFilter
{
    internal const string HeaderName = "X-Test-Client-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out StringValues header)
                && IPAddress.TryParse(header.ToString(), out IPAddress? address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            await nextMiddleware();
        });

        next(app);
    };
}
