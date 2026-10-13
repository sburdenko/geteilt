using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Geteilt.Api.Idempotency;
using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Contracts;
using Geteilt.Contracts.Groups;
using Geteilt.Contracts.Members;
using Geteilt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Geteilt.Api.Tests;

/// <summary>
/// What the API accepts and returns on the wire, written as raw JSON rather than as typed objects.
/// </summary>
/// <remarks>
/// The rest of the suite posts C# records, which the same serializer turns into JSON — so a client
/// and the server agreeing with each other proves nothing about what a client written in anything
/// else would send. This one was added after a manual curl run found that
/// <c>{"splitMethod":"Equal"}</c> was rejected while every test passed.
/// </remarks>
[Collection(SharedApiFixture.Name)]
public sealed class JsonContractTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private HttpClient _anna = null!;
    private Guid _groupId;
    private Guid _annaMember;
    private Guid _boris;

    public async Task InitializeAsync()
    {
        await factory.ResetAsync();

        RegistrationResult registered = await Use(provider =>
            provider.GetRequiredService<IIdentityService>()
                .RegisterAsync("anna@example.com", "Anna", "Passwort123", CancellationToken.None));

        AccessToken token = factory.Services.GetRequiredService<IAccessTokenService>()
            .Issue(registered.UserId, "anna@example.com", "Anna");

        _anna = factory.CreateClient();
        _anna.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        HttpResponseMessage group = await _anna.PostAsync(
            new Uri("/api/groups", UriKind.Relative), new CreateGroupRequest("Italien 2026", "EUR"));
        _groupId = (await group.Content.ReadAsync<GroupDto>())!.Id;

        HttpResponseMessage boris = await _anna.PostAsync(
            new Uri($"/api/groups/{_groupId}/members", UriKind.Relative),
            new AddMemberRequest("Boris", null, null));
        _boris = (await boris.Content.ReadAsync<MemberDto>())!.Id;

        _annaMember = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Members.Where(member => member.GroupId == _groupId && member.UserId != null)
            .Select(member => member.Id).SingleAsync());
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> Use<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        return await work(scope.ServiceProvider);
    }

    /// <summary>The wire format for a calendar date: ISO, no time, no time zone.</summary>
    private static string Today =>
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private Task<HttpResponseMessage> PostRawAsync(string path, string json)
    {
        HttpRequestMessage request = new(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        request.Headers.Add(IdempotencyFilter.HeaderName, Guid.CreateVersion7().ToString());

        return _anna.SendAsync(request);
    }

    [Fact]
    public async Task Enums_AreAcceptedByName()
    {
        string today = Today;

        HttpResponseMessage response = await PostRawAsync(
            $"/api/groups/{_groupId}/expenses",
            $$"""
            {
              "description": "Abendessen",
              "paidByMemberId": "{{_annaMember}}",
              "amount": 60.00,
              "currency": "EUR",
              "date": "{{today}}",
              "splitMethod": "Equal",
              "shares": [{ "memberId": "{{_annaMember}}" }, { "memberId": "{{_boris}}" }]
            }
            """);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Enums_AreReturnedByName()
    {
        string today = Today;

        HttpResponseMessage created = await PostRawAsync(
            $"/api/groups/{_groupId}/expenses",
            $$"""
            {
              "description": "Abendessen",
              "paidByMemberId": "{{_annaMember}}",
              "amount": 60.00,
              "currency": "EUR",
              "date": "{{today}}",
              "splitMethod": "Shares",
              "shares": [
                { "memberId": "{{_annaMember}}", "weight": 2 },
                { "memberId": "{{_boris}}", "weight": 1 }
              ]
            }
            """);

        string body = await created.Content.ReadAsStringAsync();

        // A number here would mean every client has to know the declaration order of the enum, and
        // reordering it would silently change what old stored responses mean.
        body.Should().Contain("\"splitMethod\":\"Shares\"");
    }

    [Fact]
    public async Task Money_IsANumberWithTwoDecimals()
    {
        string today = Today;

        HttpResponseMessage created = await PostRawAsync(
            $"/api/groups/{_groupId}/expenses",
            $$"""
            {
              "description": "Abendessen",
              "paidByMemberId": "{{_annaMember}}",
              "amount": 10.00,
              "currency": "EUR",
              "date": "{{today}}",
              "splitMethod": "Equal",
              "shares": [{ "memberId": "{{_annaMember}}" }, { "memberId": "{{_boris}}" }]
            }
            """);

        string body = await created.Content.ReadAsStringAsync();

        // A number, not a string: the clients are all .NET and deserialise straight into decimal
        // without losing precision.
        body.Should().Contain("\"amount\":10.00").And.NotContain("\"amount\":\"10.00\"");
    }

    [Fact]
    public async Task Dates_AreIsoCalendarDates()
    {
        string today = Today;

        HttpResponseMessage created = await PostRawAsync(
            $"/api/groups/{_groupId}/expenses",
            $$"""
            {
              "description": "Abendessen",
              "paidByMemberId": "{{_annaMember}}",
              "amount": 10.00,
              "currency": "EUR",
              "date": "{{today}}",
              "splitMethod": "Equal",
              "shares": [{ "memberId": "{{_annaMember}}" }, { "memberId": "{{_boris}}" }]
            }
            """);

        // A date without a time: an expense happens on a day, and carrying midnight in an unknown
        // time zone is where the off-by-one-day bugs come from.
        (await created.Content.ReadAsStringAsync()).Should().Contain($"\"date\":\"{today}\"");
    }

    [Fact]
    public async Task ReplayedResponses_LookExactlyLikeTheFirstOne()
    {
        string today = Today;
        string key = Guid.CreateVersion7().ToString();
        string json = $$"""
            {
              "description": "Abendessen",
              "paidByMemberId": "{{_annaMember}}",
              "amount": 10.00,
              "currency": "EUR",
              "date": "{{today}}",
              "splitMethod": "Equal",
              "shares": [{ "memberId": "{{_annaMember}}" }, { "memberId": "{{_boris}}" }]
            }
            """;

        async Task<string> SendAsync()
        {
            HttpRequestMessage request = new(
                HttpMethod.Post, new Uri($"/api/groups/{_groupId}/expenses", UriKind.Relative))
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add(IdempotencyFilter.HeaderName, key);

            return await (await _anna.SendAsync(request)).Content.ReadAsStringAsync();
        }

        string first = await SendAsync();
        string replayed = await SendAsync();

        // The stored copy is serialised with the API's own options, so a replay cannot come back
        // with enums as numbers while a fresh answer has them as names.
        replayed.Should().Be(first);
        replayed.Should().Contain("\"splitMethod\":\"Equal\"");
    }
}
