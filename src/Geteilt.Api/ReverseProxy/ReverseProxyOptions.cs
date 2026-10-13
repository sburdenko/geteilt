namespace Geteilt.Api.ReverseProxy;

/// <summary>
/// Which network the reverse proxy sits in. Only requests from there may say, in
/// <c>X-Forwarded-For</c>, who the real caller is; from anywhere else the header is ignored.
/// Empty in development, where there is no proxy and nobody is trusted.
/// </summary>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>CIDR, for example <c>172.30.0.0/24</c> — the network docker compose gives the proxy.</summary>
    public string? TrustedNetwork { get; init; }
}
