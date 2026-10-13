using System.Net;
using System.Net.Http.Json;
using Geteilt.Contracts;
using Geteilt.Contracts.Groups;
using Geteilt.Web.Auth;
using Microsoft.Extensions.Time.Testing;

namespace Geteilt.Web.Tests;

/// <summary>What the person is never supposed to see happening.</summary>
public sealed class AuthTokenHandlerTests
{
    private readonly InMemoryTokenStorage _storage = new();
    private readonly FakeTimeProvider _clock = new(Sessions.Now);

    [Fact]
    public async Task ASignedInRequest_CarriesTheAccessToken()
    {
        StubHandler stub = new((_, _) => Sessions.Status(HttpStatusCode.OK));
        HttpClient client = await BuildAsync(stub, Sessions.At("one", TimeSpan.FromMinutes(10)), _ => null);

        await client.GetAsync(new Uri("api/groups", UriKind.Relative));

        stub.Seen.Should().ContainSingle().Which.BearerToken.Should().Be("access-one");
    }

    [Fact]
    public async Task WithNobodySignedIn_NothingIsAttachedAndNothingIsRenewed()
    {
        StubHandler stub = new((_, _) => Sessions.Status(HttpStatusCode.OK));
        HttpClient client = await BuildAsync(stub, session: null, _ => null);

        await client.GetAsync(new Uri("api/groups", UriKind.Relative));

        stub.Seen.Should().ContainSingle().Which.BearerToken.Should().BeNull();
    }

    [Fact]
    public async Task AnAccessTokenAboutToExpire_IsRenewedBeforeTheRequestGoesOut()
    {
        StubHandler stub = new((_, _) => Sessions.Status(HttpStatusCode.OK));
        HttpClient client = await BuildAsync(
            stub,
            Sessions.At("one", TimeSpan.FromSeconds(5)),
            _ => Sessions.At("two", TimeSpan.FromMinutes(15)));

        await client.GetAsync(new Uri("api/groups", UriKind.Relative));

        // One request, not a failure followed by a retry: the token was replaced before it was used.
        stub.Seen.Should().ContainSingle().Which.BearerToken.Should().Be("access-two");
    }

    [Fact]
    public async Task A401OnAValidLookingToken_IsRetriedOnceWithAFreshOne()
    {
        StubHandler stub = new((_, attempt) =>
            Sessions.Status(attempt == 0 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));
        HttpClient client = await BuildAsync(
            stub,
            Sessions.At("one", TimeSpan.FromMinutes(10)),
            _ => Sessions.At("two", TimeSpan.FromMinutes(15)));

        HttpResponseMessage response = await client.GetAsync(new Uri("api/groups", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stub.Seen.Select(seen => seen.BearerToken).Should().Equal("access-one", "access-two");
    }

    [Fact]
    public async Task ARetriedPost_SendsTheSameBodyAndTheSameIdempotencyKey()
    {
        StubHandler stub = new((_, attempt) =>
            Sessions.Status(attempt == 0 ? HttpStatusCode.Unauthorized : HttpStatusCode.Created));
        HttpClient client = await BuildAsync(
            stub,
            Sessions.At("one", TimeSpan.FromMinutes(10)),
            _ => Sessions.At("two", TimeSpan.FromMinutes(15)));

        HttpRequestMessage request = new(HttpMethod.Post, new Uri("api/groups", UriKind.Relative))
        {
            Content = JsonContent.Create(new CreateGroupRequest("Italien", "EUR"), options: GeteiltJson.Options),
        };
        request.Headers.Add("Idempotency-Key", "key-42");

        await client.SendAsync(request);

        // A retry that arrived without the key would be a second group; one that arrived with an
        // empty body would be a 400. Both have to survive the renewal.
        stub.Seen.Should().HaveCount(2);
        stub.Seen[1].IdempotencyKey.Should().Be("key-42");
        stub.Seen[1].Body.Should().Be(stub.Seen[0].Body).And.Contain("Italien");
        stub.Seen[1].ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task A401ThatSurvivesTheRetry_IsHandedBackRatherThanLoopingForever()
    {
        StubHandler stub = new((_, _) => Sessions.Status(HttpStatusCode.Unauthorized));
        HttpClient client = await BuildAsync(
            stub,
            Sessions.At("one", TimeSpan.FromMinutes(10)),
            _ => Sessions.At("two", TimeSpan.FromMinutes(15)));

        HttpResponseMessage response = await client.GetAsync(new Uri("api/groups", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        stub.Seen.Should().HaveCount(2);
    }

    [Fact]
    public async Task WhenTheRefreshTokenIsRejected_TheRequestFailsAndTheSessionIsGone()
    {
        StubHandler stub = new((_, _) => Sessions.Status(HttpStatusCode.Unauthorized));
        AuthState auth = new(_storage);
        await auth.SetAsync(Sessions.At("one", TimeSpan.FromSeconds(5)));
        SessionRenewer renewer = new(auth, new CountingRefresher(_ => null), _clock);
        HttpClient client = new(new AuthTokenHandler(auth, renewer) { InnerHandler = stub })
        {
            BaseAddress = new Uri("https://api.test/"),
        };

        HttpResponseMessage response = await client.GetAsync(new Uri("api/groups", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        stub.Seen.Should().BeEmpty("a request with no usable token is not worth sending");
        auth.SignedIn.Should().BeFalse();
    }

    private async Task<HttpClient> BuildAsync(
        StubHandler stub,
        AuthSession? session,
        Func<string, AuthSession?> exchange)
    {
        AuthState auth = new(_storage);

        if (session is not null)
        {
            await auth.SetAsync(session);
        }

        SessionRenewer renewer = new(auth, new CountingRefresher(exchange), _clock);

        return new HttpClient(new AuthTokenHandler(auth, renewer) { InnerHandler = stub })
        {
            BaseAddress = new Uri("https://api.test/"),
        };
    }
}
