using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace Geteilt.Api.Tests;

/// <summary>
/// Without a usable IBAN key the API refuses to start, rather than failing on the first
/// write of an IBAN or, worse, storing one unprotected.
/// </summary>
public sealed class IbanKeyConfigurationTests
{
    private static WebApplicationFactory<Program> FactoryWith(string ibanKey) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=none;Username=none;Password=none");
            builder.UseSetting("Jwt:Key", GeteiltApiFactory.TestSigningKey);
            builder.UseSetting("Jwt:Issuer", GeteiltApiFactory.TestIssuer);
            builder.UseSetting("Jwt:Audience", GeteiltApiFactory.TestAudience);
            builder.UseSetting("IbanProtection:Key", ibanKey);
        });

    [Theory]
    [InlineData("")]
    [InlineData("c2hvcnQ=")]
    public void Startup_WithoutA32ByteIbanKey_Fails(string ibanKey)
    {
        using WebApplicationFactory<Program> factory = FactoryWith(ibanKey);

        Action act = () => factory.CreateClient();

        act.Should().Throw<OptionsValidationException>().WithMessage("*IbanProtection:Key*");
    }

    [Fact]
    public void Startup_WithA32ByteIbanKey_Succeeds()
    {
        using WebApplicationFactory<Program> factory = FactoryWith(GeteiltApiFactory.TestIbanKey);

        Action act = () => factory.CreateClient();

        act.Should().NotThrow();
    }
}
