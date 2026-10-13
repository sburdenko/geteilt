using System.Net;
using System.Text;
using Geteilt.Contracts.Groups;
using Geteilt.Web.Api;

namespace Geteilt.Web.Tests;

/// <summary>
/// An API error has to reach the toast as a readable title. The bodies here are raw JSON on
/// purpose: a test that posts a typed record through our own serialiser agrees with itself no
/// matter what the wire format actually is.
/// </summary>
public sealed class ApiCallTests
{
    [Fact]
    public async Task AProblemDocument_BecomesAnErrorWithItsTitleAndCode()
    {
        GroupsApi api = Answering(
            HttpStatusCode.UnprocessableEntity,
            """
            {
              "type": "https://geteilt.app/errors/validation-failed",
              "title": "Die Eingaben sind unvollständig",
              "status": 422,
              "detail": "Der Name fehlt.",
              "code": "validation_failed",
              "errors": { "name": ["Pflichtfeld"] }
            }
            """);

        Result<GroupDto> result = await api.CreateAsync(new CreateGroupRequest("", "EUR"));

        ApiError error = Failure(result);
        error.Status.Should().Be(422);
        error.Code.Should().Be("validation_failed");
        error.Title.Should().Be("Die Eingaben sind unvollständig");
        error.Detail.Should().Be("Der Name fehlt.");
        error.FieldErrors["name"].Should().Equal("Pflichtfeld");
    }

    [Fact]
    public async Task AProblemDocumentWithoutATitle_StillGivesTheToastSomethingToSay()
    {
        GroupsApi api = Answering(HttpStatusCode.Conflict, """{"status":409,"code":"conflict"}""");

        Failure(await api.ListAsync()).Title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AnErrorThatIsNotJsonAtAll_IsReportedRatherThanThrown()
    {
        // nginx and the dev server both answer with HTML when they, not the API, are the problem.
        GroupsApi api = Answering(HttpStatusCode.BadGateway, "<html><body>502</body></html>", "text/html");

        ApiError error = Failure(await api.ListAsync());
        error.Status.Should().Be(502);
        error.Code.Should().Be(ApiError.UnreadableCode);
    }

    [Fact]
    public async Task AServerThatCannotBeReached_IsAnOutcomeAndNotAnException()
    {
        GroupsApi api = new(new HttpClient(
            new ThrowingHandler(new HttpRequestException("Failed to fetch")))
        {
            BaseAddress = new Uri("https://api.test/"),
        });

        Failure(await api.ListAsync()).Code.Should().Be(ApiError.UnreachableCode);
    }

    [Fact]
    public async Task ASuccessfulCall_CarriesTheValue()
    {
        GroupsApi api = Answering(
            HttpStatusCode.OK,
            """
            [{"id":"0198f000-0000-7000-8000-000000000009","name":"Italien 2026","baseCurrency":"EUR",
              "createdAt":"2026-08-01T10:00:00+00:00","archivedAt":null,"memberCount":3,"myBalance":30.00}]
            """);

        Result<IReadOnlyList<GroupDto>> result = await api.ListAsync();

        result.Match(groups => groups.Single().Name, error => error.Title).Should().Be("Italien 2026");
        result.Match(groups => groups.Single().MyBalance, _ => 0m).Should().Be(30.00m);
    }

    [Fact]
    public async Task EveryPost_CarriesAnIdempotencyKey()
    {
        StubHandler stub = new((_, _) => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(
                """
                {"id":"0198f000-0000-7000-8000-000000000009","name":"Italien","baseCurrency":"EUR",
                 "createdAt":"2026-08-01T10:00:00+00:00","archivedAt":null,"memberCount":1,"myBalance":0}
                """,
                Encoding.UTF8,
                "application/json"),
        });
        GroupsApi api = new(new HttpClient(stub) { BaseAddress = new Uri("https://api.test/") });

        await api.CreateAsync(new CreateGroupRequest("Italien", "EUR"));

        // Without one the API answers 400, so a missing key is a broken client.
        stub.Seen.Single().IdempotencyKey.Should().NotBeNullOrWhiteSpace();
    }

    private static GroupsApi Answering(HttpStatusCode status, string body, string mediaType = "application/problem+json") =>
        new(new HttpClient(new StubHandler((_, _) => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        }))
        {
            BaseAddress = new Uri("https://api.test/"),
        });

    private static ApiError Failure<T>(Result<T> result)
    {
        result.Should().BeOfType<Result<T>.Failed>();

        return ((Result<T>.Failed)result).Error;
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
