using Geteilt.Api.Cors;

namespace Geteilt.Api.Tests;

public sealed class LocalNetworkOriginsTests
{
    private static readonly string[] Configured = ["http://localhost:5088"];

    [Theory]
    [InlineData("http://localhost:5088")]
    [InlineData("http://localhost:1234")]
    [InlineData("http://127.0.0.1:5088")]
    [InlineData("http://192.168.0.187:5088")]
    [InlineData("http://10.0.0.5:5088")]
    [InlineData("http://172.16.4.1:5088")]
    [InlineData("http://169.254.10.2:5088")]
    [InlineData("http://mbp.local:5088")]
    [InlineData("https://192.168.1.20:5088")]
    public void AnythingOnTheLocalNetwork_IsAllowed(string origin) =>
        LocalNetworkOrigins.Allows(origin, Configured).Should().BeTrue();

    [Theory]
    [InlineData("https://geteilt.de")]
    [InlineData("http://93.184.216.34")]
    [InlineData("http://172.32.0.1:5088", "just outside the private range")]
    [InlineData("http://192.169.0.1:5088", "one off from 192.168")]
    [InlineData("file:///Users/somebody/evil.html", "not even a network origin")]
    [InlineData("not a url")]
    public void AnythingElse_IsNot(string origin, string? because = null) =>
        LocalNetworkOrigins.Allows(origin, Configured).Should().BeFalse(because ?? string.Empty);

    [Fact]
    public void AnOriginSomebodyConfigured_IsAllowedWhereverItPoints()
    {
        // The list stays meaningful: a tunnel or a staging host can be added by hand without
        // widening the rule for everyone.
        LocalNetworkOrigins.Allows("https://tunnel.example.com", ["https://tunnel.example.com"])
            .Should().BeTrue();
    }
}
