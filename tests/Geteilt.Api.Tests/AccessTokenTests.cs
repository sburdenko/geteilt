using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Contracts;
using Geteilt.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Geteilt.Api.Tests;

/// <summary>Access tokens: what they carry, how long they live, and what they are refused for.</summary>
[Collection(SharedApiFixture.Name)]
public sealed class AccessTokenTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private static readonly Guid UserId = new("01920000-0000-7000-8000-000000000001");

    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private AccessToken Issue() =>
        factory.Services.GetRequiredService<IAccessTokenService>()
            .Issue(UserId, "anna@example.com", "Anna");

    private static JsonWebToken Read(string token) => new(token);

    private HttpClient ClientWith(string? token)
    {
        HttpClient client = factory.CreateClient();

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    [Fact]
    public void Issue_PutsTheAgreedClaimsInTheToken()
    {
        JsonWebToken token = Read(Issue().Value);

        token.Subject.Should().Be(UserId.ToString());
        token.GetClaim(JwtRegisteredClaimNames.Email).Value.Should().Be("anna@example.com");
        token.GetClaim(JwtRegisteredClaimNames.Name).Value.Should().Be("Anna");
        token.Id.Should().NotBeEmpty();
        token.Issuer.Should().Be(GeteiltApiFactory.TestIssuer);
        token.Audiences.Should().Contain(GeteiltApiFactory.TestAudience);
    }

    [Fact]
    public void Issue_GivesTheTokenFifteenMinutes()
    {
        AccessToken issued = Issue();
        JsonWebToken token = Read(issued.Value);

        (token.ValidTo - token.ValidFrom).Should().Be(TimeSpan.FromMinutes(15));
        issued.ExpiresAt.Should().BeCloseTo(token.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Issue_KeepsGroupMembershipOutOfTheToken()
    {
        // Membership changes and an issued token cannot be withdrawn, so authorisation reads
        // membership from the database on every request instead.
        JsonWebToken token = Read(Issue().Value);

        token.Claims.Should().NotContain(claim =>
            claim.Type.Contains("group", StringComparison.OrdinalIgnoreCase)
            || claim.Type == "role");
    }

    [Fact]
    public void Issue_TwiceForTheSameUser_GivesDifferentTokenIds()
    {
        Read(Issue().Value).Id.Should().NotBe(Read(Issue().Value).Id);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutAToken_Answers401()
    {
        HttpResponseMessage response = await ClientWith(null).GetAsync(new Uri("/api/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAValidToken_Answers200()
    {
        RegistrationResult registered = await RegisterAsync();
        AccessToken token = factory.Services.GetRequiredService<IAccessTokenService>()
            .Issue(registered.UserId, "anna@example.com", "Anna");

        HttpResponseMessage response = await ClientWith(token.Value).GetAsync(new Uri("/api/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        UserDto? user = await response.Content.ReadAsync<UserDto>();
        user!.Email.Should().Be("anna@example.com");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAnExpiredToken_Answers401()
    {
        string expired = SignedWith(GeteiltApiFactory.TestSigningKey, expiresIn: TimeSpan.FromMinutes(-1));

        HttpResponseMessage response = await ClientWith(expired).GetAsync(new Uri("/api/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithATokenSignedByAnotherKey_Answers401()
    {
        // Whoever knows the key can mint any token they like, which is why the key never enters git.
        string forged = SignedWith("a-completely-different-key-also-long-enough", TimeSpan.FromMinutes(15));

        HttpResponseMessage response = await ClientWith(forged).GetAsync(new Uri("/api/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithATokenForAnotherAudience_Answers401()
    {
        string wrongAudience = SignedWith(
            GeteiltApiFactory.TestSigningKey, TimeSpan.FromMinutes(15), audience: "https://somewhere.else");

        HttpResponseMessage response = await ClientWith(wrongAudience).GetAsync(new Uri("/api/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string SignedWith(string key, TimeSpan expiresIn, string? audience = null)
    {
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = GeteiltApiFactory.TestIssuer,
            Audience = audience ?? GeteiltApiFactory.TestAudience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, UserId.ToString())]),
            Expires = DateTime.UtcNow.Add(expiresIn),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private async Task<RegistrationResult> RegisterAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IIdentityService>()
            .RegisterAsync("anna@example.com", "Anna", "Passwort123", CancellationToken.None);
    }
}
