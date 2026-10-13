using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Geteilt.Api.Idempotency;
using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Contracts;
using Geteilt.Contracts.Groups;
using Geteilt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Geteilt.Api.Tests;

/// <summary>A repeated POST with the same Idempotency-Key is answered from memory, not run again.</summary>
[Collection(SharedApiFixture.Name)]
public sealed class IdempotencyTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private static readonly Uri Groups = new("/api/groups", UriKind.Relative);

    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> Use<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        return await work(scope.ServiceProvider);
    }

    private async Task<HttpClient> SignInAsync()
    {
        RegistrationResult registered = await Use(provider =>
            provider.GetRequiredService<IIdentityService>()
                .RegisterAsync("anna@example.com", "Anna", "Passwort123", CancellationToken.None));

        AccessToken token = factory.Services.GetRequiredService<IAccessTokenService>()
            .Issue(registered.UserId, "anna@example.com", "Anna");

        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        return client;
    }

    private static HttpRequestMessage Post(string body, string? key)
    {
        HttpRequestMessage request = new(HttpMethod.Post, Groups)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (key is not null)
        {
            request.Headers.Add(IdempotencyFilter.HeaderName, key);
        }

        return request;
    }

    private const string Body = """{"name":"Italien 2026","baseCurrency":"EUR"}""";

    private Task<int> CountGroupsAsync() =>
        Use(provider => provider.GetRequiredService<AppDbContext>().Groups.CountAsync());

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadAsync<Dictionary<string, object>>())
            ?.GetValueOrDefault("code")?.ToString();

    [Fact]
    public async Task Post_WithoutTheHeader_Answers400()
    {
        HttpClient client = await SignInAsync();

        HttpResponseMessage response = await client.SendAsync(Post(Body, key: null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeOf(response)).Should().Be("idempotency_key_required");
        (await CountGroupsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Post_WithAKey_Works()
    {
        HttpClient client = await SignInAsync();

        HttpResponseMessage response = await client.SendAsync(Post(Body, Guid.CreateVersion7().ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await CountGroupsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Post_RepeatedWithTheSameKeyAndBody_ReplaysTheAnswerAndCreatesNothing()
    {
        HttpClient client = await SignInAsync();
        string key = Guid.CreateVersion7().ToString();

        HttpResponseMessage first = await client.SendAsync(Post(Body, key));
        HttpResponseMessage second = await client.SendAsync(Post(Body, key));

        // This is the phone on the underground: the expense went through, the answer never
        // arrived, the client retried.
        second.StatusCode.Should().Be(first.StatusCode);
        (await second.Content.ReadAsStringAsync())
            .Should().Be(await first.Content.ReadAsStringAsync());

        (await CountGroupsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Post_RepeatedWithTheSameKeyAndADifferentBody_Answers409()
    {
        HttpClient client = await SignInAsync();
        string key = Guid.CreateVersion7().ToString();

        await client.SendAsync(Post(Body, key));

        HttpResponseMessage response = await client.SendAsync(
            Post("""{"name":"Etwas anderes","baseCurrency":"EUR"}""", key));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(response)).Should().Be("idempotency_key_reuse");
        (await CountGroupsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Post_WithADifferentKey_CreatesASecondGroup()
    {
        HttpClient client = await SignInAsync();

        await client.SendAsync(Post(Body, Guid.CreateVersion7().ToString()));
        await client.SendAsync(Post(Body, Guid.CreateVersion7().ToString()));

        // Identical bodies, different keys: the client meant it twice.
        (await CountGroupsAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Hash_IsTakenFromTheRawBytesNotTheParsedObject()
    {
        HttpClient client = await SignInAsync();
        string key = Guid.CreateVersion7().ToString();

        await client.SendAsync(Post(Body, key));

        // Same object once parsed — different whitespace and field order — but different bytes.
        // Hashing the deserialised object would call these equal and replay the first answer.
        HttpResponseMessage response = await client.SendAsync(
            Post("""{ "baseCurrency" : "EUR", "name" : "Italien 2026" }""", key));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(response)).Should().Be("idempotency_key_reuse");
    }

    [Fact]
    public async Task Keys_AreScopedToOnePerson()
    {
        HttpClient anna = await SignInAsync();

        RegistrationResult boris = await Use(provider =>
            provider.GetRequiredService<IIdentityService>()
                .RegisterAsync("boris@example.com", "Boris", "Passwort123", CancellationToken.None));

        HttpClient borisClient = factory.CreateClient();
        borisClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.Services.GetRequiredService<IAccessTokenService>()
                .Issue(boris.UserId, "boris@example.com", "Boris").Value);

        string sharedKey = Guid.CreateVersion7().ToString();

        await anna.SendAsync(Post(Body, sharedKey));
        HttpResponseMessage borisResponse = await borisClient.SendAsync(Post(Body, sharedKey));

        // One client must not be able to occupy — or collide with — another's key.
        borisResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await CountGroupsAsync()).Should().Be(2);
    }

    [Fact]
    public async Task FailedRequests_AreNotRemembered()
    {
        HttpClient client = await SignInAsync();
        string key = Guid.CreateVersion7().ToString();

        HttpResponseMessage rejected = await client.SendAsync(
            Post("""{"name":"Nirgendwo","baseCurrency":"XYZ"}""", key));
        rejected.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Replaying a failure would freeze a transient problem into a permanent one — the retry
        // meant to fix it would receive the old error.
        HttpResponseMessage retried = await client.SendAsync(Post(Body, key));

        retried.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AuthEndpoints_DoNotRequireAKey()
    {
        HttpResponseMessage response = await factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            new Contracts.Auth.RegisterRequest("neu@example.com", "Passwort123", "Neu"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Cleanup_RemovesRecordsPastTheirTtlAndKeepsTheRest()
    {
        HttpClient client = await SignInAsync();
        await client.SendAsync(Post(Body, Guid.CreateVersion7().ToString()));

        (await CountRecordsAsync()).Should().Be(1);

        // Aged in the database rather than by moving the clock: the host is shared with every
        // other test in this collection.
        await Use(async provider => await provider.GetRequiredService<AppDbContext>()
            .Database.ExecuteSqlRawAsync(
                "UPDATE idempotency_records SET created_at = now() - interval '25 hours';"));

        int removed = await factory.Services.GetServices<IHostedService>()
            .OfType<IdempotencyCleanupService>()
            .Single()
            .SweepAsync(CancellationToken.None);

        removed.Should().Be(1);
        (await CountRecordsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cleanup_LeavesRecordsThatAreStillYoung()
    {
        HttpClient client = await SignInAsync();
        await client.SendAsync(Post(Body, Guid.CreateVersion7().ToString()));

        int removed = await factory.Services.GetServices<IHostedService>()
            .OfType<IdempotencyCleanupService>()
            .Single()
            .SweepAsync(CancellationToken.None);

        removed.Should().Be(0);
        (await CountRecordsAsync()).Should().Be(1);
    }

    private Task<int> CountRecordsAsync() =>
        Use(provider => provider.GetRequiredService<AppDbContext>().IdempotencyRecords.CountAsync());
}
