using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace Geteilt.Api.Tests;

/// <summary>
/// Configuration is validated with <c>ValidateOnStart</c>, so a misconfigured deployment fails
/// while somebody is still watching the deploy — not on the first request two hours later
///.
/// </summary>
public sealed class JwtConfigurationTests
{
    private static WebApplicationFactory<Program> FactoryWith(string key) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=none;Username=none;Password=none");
            builder.UseSetting("Jwt:Key", key);
            builder.UseSetting("Jwt:Issuer", GeteiltApiFactory.TestIssuer);
            builder.UseSetting("Jwt:Audience", GeteiltApiFactory.TestAudience);
            builder.UseSetting("IbanProtection:Key", GeteiltApiFactory.TestIbanKey);
        });

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("exactly-31-characters-long-abc!")]
    public void Startup_WithAKeyShorterThanThirtyTwoCharacters_Fails(string key)
    {
        using WebApplicationFactory<Program> factory = FactoryWith(key);

        Action act = () => factory.CreateClient();

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Startup_WithALongEnoughKey_Succeeds()
    {
        using WebApplicationFactory<Program> factory = FactoryWith(GeteiltApiFactory.TestSigningKey);

        Action act = () => factory.CreateClient();

        act.Should().NotThrow();
    }
}
