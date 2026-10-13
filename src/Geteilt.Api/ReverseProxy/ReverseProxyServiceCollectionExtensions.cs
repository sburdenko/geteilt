using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Geteilt.Api.ReverseProxy;

internal static class ReverseProxyServiceCollectionExtensions
{
    /// <summary>
    /// Behind Caddy every request arrives from Caddy. Without this the login limit, which counts per
    /// address, would count the whole world as one caller.
    /// </summary>
    internal static IServiceCollection AddGeteiltReverseProxy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ReverseProxyOptions proxy = configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>()
            ?? new ReverseProxyOptions();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // One hop: Caddy. Anything further left in the header was written by the caller.
            options.ForwardLimit = 1;

            // The defaults trust loopback. Nothing is trusted unless configured, so a header sent
            // straight to the API by anyone else changes nothing.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            if (!string.IsNullOrWhiteSpace(proxy.TrustedNetwork))
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(proxy.TrustedNetwork));
            }
        });

        return services;
    }
}
