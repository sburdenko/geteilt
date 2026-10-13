using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Geteilt.Contracts;
using Geteilt.Contracts.Auth;
using Geteilt.Contracts.Users;

namespace Geteilt.Api.Tests;

/// <summary>The whole session lifecycle over HTTP.</summary>
[Collection(SharedApiFixture.Name)]
public sealed class AuthEndpointTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private const string Password = "Passwort123";

    private static readonly Uri Register = new("/api/auth/register", UriKind.Relative);
    private static readonly Uri Login = new("/api/auth/login", UriKind.Relative);
    private static readonly Uri Refresh = new("/api/auth/refresh", UriKind.Relative);
    private static readonly Uri Logout = new("/api/auth/logout", UriKind.Relative);
    private static readonly Uri Me = new("/api/me", UriKind.Relative);

    private static int _addressCounter;

    /// <summary>
    /// A fresh address per client: the login rate limit partitions by IP, and without this one
    /// test's failed attempts would count against the next one.
    /// </summary>
    private HttpClient CreateClient()
    {
        HttpClient client = factory.CreateClient();
        int index = Interlocked.Increment(ref _addressCounter);

        client.DefaultRequestHeaders.Add(
            TestClientIpStartupFilter.HeaderName,
            string.Create(CultureInfo.InvariantCulture, $"198.51.100.{(index % 250) + 1}"));

        return client;
    }

    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string email = "anna@example.com")
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            Register, new RegisterRequest(email, Password, "Anna"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadAsync<AuthResponse>())!;
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        Dictionary<string, object>? problem =
            await response.Content.ReadAsync<Dictionary<string, object>>();

        return problem?.GetValueOrDefault("code")?.ToString();
    }

    private static HttpClient Authorised(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    [Fact]
    public async Task Register_ReturnsBothTokensAndTheUser()
    {
        AuthResponse auth = await RegisterAsync(CreateClient());

        auth.AccessToken.Should().NotBeEmpty();
        auth.RefreshToken.Should().NotBeEmpty();
        auth.AccessTokenExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        auth.RefreshTokenExpiresAt.Should().BeAfter(auth.AccessTokenExpiresAt);
        auth.User.Email.Should().Be("anna@example.com");
        auth.User.DisplayName.Should().Be("Anna");
        auth.User.PreferredCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Register_OnATakenEmail_Answers409()
    {
        HttpClient client = CreateClient();
        await RegisterAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            Register, new RegisterRequest("anna@example.com", Password, "Anna"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(response)).Should().Be("email_already_registered");
    }

    [Fact]
    public async Task Register_WithAWeakPassword_Answers422()
    {
        HttpResponseMessage response = await CreateClient().PostAsJsonAsync(
            Register, new RegisterRequest("anna@example.com", "kurz", "Anna"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_WithTheRightPassword_ReturnsTokens()
    {
        HttpClient client = CreateClient();
        await RegisterAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            Login, new LoginRequest("anna@example.com", Password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsync<AuthResponse>())!.AccessToken.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_WithAWrongPassword_Answers401()
    {
        HttpClient client = CreateClient();
        await RegisterAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            Login, new LoginRequest("anna@example.com", "falsches Passwort"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeOf(response)).Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Login_TellsTheSameStoryForAWrongPasswordAndAnUnknownAddress()
    {
        HttpClient client = CreateClient();
        await RegisterAsync(client);

        HttpResponseMessage wrongPassword = await client.PostAsJsonAsync(
            Login, new LoginRequest("anna@example.com", "falsches Passwort"));
        HttpResponseMessage unknownAddress = await client.PostAsJsonAsync(
            Login, new LoginRequest("niemand@example.com", Password));

        // Otherwise the login form doubles as a way to find out who has an account here.
        unknownAddress.StatusCode.Should().Be(wrongPassword.StatusCode);
        (await unknownAddress.Content.ReadAsStringAsync())
            .Should().Be(await wrongPassword.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Refresh_ReturnsANewPairAndRetiresTheOldRefreshToken()
    {
        HttpClient client = CreateClient();
        AuthResponse first = await RegisterAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            Refresh, new RefreshRequest(first.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthResponse second = (await response.Content.ReadAsync<AuthResponse>())!;
        second.RefreshToken.Should().NotBe(first.RefreshToken);

        HttpResponseMessage reused = await client.PostAsJsonAsync(
            Refresh, new RefreshRequest(first.RefreshToken));
        reused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_Answers401()
    {
        HttpResponseMessage response = await CreateClient()
            .PostAsJsonAsync(Refresh, new RefreshRequest("never-issued"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_MakesTheRefreshTokenUnusable()
    {
        HttpClient client = CreateClient();
        AuthResponse auth = await RegisterAsync(client);

        HttpResponseMessage loggedOut = await client.PostAsJsonAsync(Logout, new RefreshRequest(auth.RefreshToken));
        loggedOut.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage afterwards = await client.PostAsJsonAsync(Refresh, new RefreshRequest(auth.RefreshToken));
        afterwards.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithAnUnknownToken_StillAnswers204()
    {
        HttpResponseMessage response = await CreateClient()
            .PostAsJsonAsync(Logout, new RefreshRequest("never-issued"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Patch_UpdatesTheProfile()
    {
        HttpClient client = CreateClient();
        AuthResponse auth = await RegisterAsync(client);

        HttpResponseMessage response = await Authorised(client, auth.AccessToken)
            .PatchAsync(Me, new UpdateMeRequest("Anna Weber", "DE89 3704 0044 0532 0130 00", "CHF"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        UserDto user = (await response.Content.ReadAsync<UserDto>())!;
        user.DisplayName.Should().Be("Anna Weber");
        user.Iban.Should().Be("DE89370400440532013000");
        user.PreferredCurrency.Should().Be("CHF");
    }

    [Fact]
    public async Task Patch_LeavesFieldsThatWereNotSent()
    {
        HttpClient client = CreateClient();
        AuthResponse auth = await RegisterAsync(client);
        HttpClient authorised = Authorised(client, auth.AccessToken);

        await authorised.PatchAsync(Me, new UpdateMeRequest(null, "DE89370400440532013000", null));
        HttpResponseMessage response = await authorised.PatchAsync(Me, new UpdateMeRequest("Nur der Name", null, null));

        UserDto user = (await response.Content.ReadAsync<UserDto>())!;
        user.DisplayName.Should().Be("Nur der Name");
        user.Iban.Should().Be("DE89370400440532013000");
    }

    [Fact]
    public async Task Patch_WithAnInvalidIban_Answers422()
    {
        HttpClient client = CreateClient();
        AuthResponse auth = await RegisterAsync(client);

        HttpResponseMessage response = await Authorised(client, auth.AccessToken)
            .PatchAsync(Me, new UpdateMeRequest(null, "DE89370400440532013001", null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("invalid_iban");
    }

    [Fact]
    public async Task Patch_WithAnUnsupportedCurrency_Answers422()
    {
        HttpClient client = CreateClient();
        AuthResponse auth = await RegisterAsync(client);

        HttpResponseMessage response = await Authorised(client, auth.AccessToken)
            .PatchAsync(Me, new UpdateMeRequest(null, null, "XYZ"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("invalid_currency");
    }

    [Fact]
    public async Task Patch_WithoutAToken_Answers401()
    {
        HttpResponseMessage response = await CreateClient()
            .PatchAsync(Me, new UpdateMeRequest("Anna", null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_AfterFiveAttemptsFromOneAddress_Answers429()
    {
        HttpClient client = CreateClient();
        await RegisterAsync(client);

        List<HttpStatusCode> statuses = [];
        for (int attempt = 0; attempt < 6; attempt++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync(
                Login, new LoginRequest("anna@example.com", "falsches Passwort"));
            statuses.Add(response.StatusCode);
        }

        statuses.Take(5).Should().AllSatisfy(status => status.Should().NotBe(HttpStatusCode.TooManyRequests));
        statuses[5].Should().Be(HttpStatusCode.TooManyRequests);
    }
}
