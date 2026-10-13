using System.Net;
using System.Net.Http.Json;
using Geteilt.Contracts.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Geteilt.Api.Tests;

/// <summary>
/// Behind Caddy every request arrives from the proxy. The login limit has to
/// keep counting per caller, and only the proxy may say who the caller is.
/// </summary>
[Collection(SharedApiFixture.Name)]
public sealed class ReverseProxyTests(GeteiltApiFactory factory)
{
    private const string ProxyNetwork = "172.30.0.0/24";
    private const string Proxy = "172.30.0.10";

    private static readonly Uri Login = new("/api/auth/login", UriKind.Relative);

    [Fact]
    public async Task BehindTheTrustedProxy_EachCallerHasALoginLimitOfTheirOwn()
    {
        using WebApplicationFactory<Program> behindProxy = BehindProxy();

        HttpStatusCode[] first = await AttemptsAsync(behindProxy, from: Proxy, forwardedFor: "203.0.113.5", count: 6);
        HttpStatusCode[] second = await AttemptsAsync(behindProxy, from: Proxy, forwardedFor: "203.0.113.6", count: 1);

        first[5].Should().Be(HttpStatusCode.TooManyRequests);
        second[0].Should().NotBe(HttpStatusCode.TooManyRequests, "another caller behind the same proxy is not throttled");
    }

    [Fact]
    public async Task FromOutsideTheProxyNetwork_AForwardedHeaderIsIgnored()
    {
        // Otherwise anybody could pick a fresh bucket for every attempt by inventing the header.
        using WebApplicationFactory<Program> behindProxy = BehindProxy();

        List<HttpStatusCode> statuses = [];
        for (int attempt = 0; attempt < 6; attempt++)
        {
            statuses.AddRange(await AttemptsAsync(
                behindProxy, from: "198.51.100.7", forwardedFor: $"203.0.113.{attempt + 20}", count: 1));
        }

        statuses[5].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task OnlyTheHopTheProxyAdded_Counts_NotWhatTheCallerWroteBeforeIt()
    {
        // Caddy appends the caller's address to whatever X-Forwarded-For the caller sent. Reading
        // further left than one hop would let a caller choose a fresh bucket for every attempt.
        using WebApplicationFactory<Program> behindProxy = BehindProxy();

        List<HttpStatusCode> statuses = [];
        for (int attempt = 0; attempt < 6; attempt++)
        {
            statuses.AddRange(await AttemptsAsync(
                behindProxy, from: Proxy, forwardedFor: $"10.9.9.{attempt}, 203.0.113.5", count: 1));
        }

        statuses[5].Should().Be(HttpStatusCode.TooManyRequests);
    }

    private WebApplicationFactory<Program> BehindProxy() =>
        factory.WithWebHostBuilder(builder => builder.UseSetting("ReverseProxy:TrustedNetwork", ProxyNetwork));

    private static async Task<HttpStatusCode[]> AttemptsAsync(
        WebApplicationFactory<Program> app,
        string from,
        string forwardedFor,
        int count)
    {
        using HttpClient client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.HeaderName, from);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);

        List<HttpStatusCode> statuses = [];
        for (int attempt = 0; attempt < count; attempt++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync(
                Login, new LoginRequest("nobody@example.com", "falsches Passwort"));
            statuses.Add(response.StatusCode);
        }

        return [.. statuses];
    }
}
