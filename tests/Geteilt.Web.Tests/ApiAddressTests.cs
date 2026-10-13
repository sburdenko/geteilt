using Geteilt.Web;

namespace Geteilt.Web.Tests;

public sealed class ApiAddressTests
{
    [Fact]
    public void TheHostPlaceholder_BecomesWhereverTheAppWasServedFrom()
    {
        // The point of the whole thing: one setting that works on the laptop and on a phone,
        // where "localhost" would mean the phone itself.
        ApiAddress.Resolve("http://{host}:8080/", "http://192.168.0.187:5088/")
            .Should().Be(new Uri("http://192.168.0.187:8080/"));

        ApiAddress.Resolve("http://{host}:8080/", "http://localhost:5088/")
            .Should().Be(new Uri("http://localhost:8080/"));
    }

    [Fact]
    public void AnAddressWithoutThePlaceholder_IsTakenAsWritten() =>
        ApiAddress.Resolve("https://api.geteilt.de/", "https://app.geteilt.de/")
            .Should().Be(new Uri("https://api.geteilt.de/"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingConfigured_MeansTheApiServesTheAppItself(string? configured) =>
        ApiAddress.Resolve(configured, "https://geteilt.de/")
            .Should().Be(new Uri("https://geteilt.de/"));

    [Fact]
    public void ThePortComesFromConfiguration_NotFromThePageTheAppWasServedFrom() =>
        ApiAddress.Resolve("http://{host}:8080/", "http://192.168.0.187:5088/")
            .Port.Should().Be(8080);
}
