using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Geteilt.Api.Tests.Diagnostics;
using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Contracts;
using Geteilt.Contracts.Expenses;
using Geteilt.Contracts.Groups;
using Geteilt.Contracts.Members;
using Geteilt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Geteilt.Api.Tests;

/// <summary>Keyset paging over the expense feed.</summary>
[Collection(SharedApiFixture.Name)]
public sealed class ExpenseFeedTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private HttpClient _anna = null!;
    private GroupDto _group = null!;
    private Guid _annaMemberId;
    private Guid _borisMemberId;

    public async Task InitializeAsync()
    {
        await factory.ResetAsync();

        (_anna, _) = await SignInAsync("anna@example.com");

        HttpResponseMessage created = await _anna.PostAsync(
            new Uri("/api/groups", UriKind.Relative), new CreateGroupRequest("Italien 2026", "EUR"));
        _group = (await created.Content.ReadAsync<GroupDto>())!;

        HttpResponseMessage boris = await _anna.PostAsync(
            new Uri($"/api/groups/{_group.Id}/members", UriKind.Relative),
            new AddMemberRequest("Boris", null, null));
        _borisMemberId = (await boris.Content.ReadAsync<MemberDto>())!.Id;

        _annaMemberId = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Members.Where(member => member.GroupId == _group.Id && member.UserId != null)
            .Select(member => member.Id).SingleAsync());
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> Use<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        return await work(scope.ServiceProvider);
    }

    private async Task<(HttpClient Client, Guid UserId)> SignInAsync(string email)
    {
        RegistrationResult registered = await Use(provider =>
            provider.GetRequiredService<IIdentityService>()
                .RegisterAsync(email, email.Split('@')[0], "Passwort123", CancellationToken.None));

        AccessToken token = factory.Services.GetRequiredService<IAccessTokenService>()
            .Issue(registered.UserId, email, email.Split('@')[0]);

        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        return (client, registered.UserId);
    }

    private Uri Feed(string? cursor = null, int? limit = null)
    {
        List<string> query = [];

        if (cursor is not null)
        {
            query.Add($"cursor={Uri.EscapeDataString(cursor)}");
        }

        if (limit is not null)
        {
            query.Add(string.Create(CultureInfo.InvariantCulture, $"limit={limit}"));
        }

        string suffix = query.Count == 0 ? string.Empty : "?" + string.Join('&', query);

        return new Uri($"/api/groups/{_group.Id}/expenses{suffix}", UriKind.Relative);
    }

    private Task<HttpResponseMessage> AddAsync(string description, DateOnly date) =>
        _anna.PostAsync(
            new Uri($"/api/groups/{_group.Id}/expenses", UriKind.Relative),
            new CreateExpenseRequest(
                description,
                _annaMemberId,
                10.00m,
                "EUR",
                date,
                SplitMethod.Equal,
                [new ShareInputDto(_annaMemberId, null, null), new ShareInputDto(_borisMemberId, null, null)]));

    private async Task SeedAsync(int count)
    {
        DateOnly start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-count);

        for (int index = 0; index < count; index++)
        {
            await AddAsync(
                string.Create(CultureInfo.InvariantCulture, $"Ausgabe {index:D3}"),
                start.AddDays(index));
        }
    }

    [Fact]
    public async Task Feed_ReturnsNewestFirst()
    {
        await SeedAsync(5);

        ExpensePage page = (await _anna.GetAsync<ExpensePage>(Feed()))!;

        page.Items.Should().HaveCount(5);
        page.Items.Select(expense => expense.Date).Should().BeInDescendingOrder();
        page.Items[0].Description.Should().Be("Ausgabe 004");
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Feed_WalksEveryPageWithoutRepeatingOrSkipping()
    {
        await SeedAsync(12);

        List<Guid> seen = [];
        string? cursor = null;

        do
        {
            ExpensePage page = (await _anna.GetAsync<ExpensePage>(Feed(cursor, limit: 5)))!;
            seen.AddRange(page.Items.Select(expense => expense.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        seen.Should().HaveCount(12);
        seen.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Feed_WhenAnExpenseIsAddedMidWalk_StillSeesEveryOlderOneExactlyOnce()
    {
        // The reason keyset beats offset. With OFFSET, inserting at the top shifts every later row
        // by one: the reader gets one entry twice and misses another. Somebody will insert, because
        // everyone is sitting at the same table.
        await SeedAsync(100);

        ExpensePage first = (await _anna.GetAsync<ExpensePage>(Feed(limit: 20)))!;
        first.Items.Should().HaveCount(20);

        await AddAsync("Nachzuegler", DateOnly.FromDateTime(DateTime.UtcNow));

        List<Guid> seen = [.. first.Items.Select(expense => expense.Id)];
        string? cursor = first.NextCursor;

        while (cursor is not null)
        {
            ExpensePage page = (await _anna.GetAsync<ExpensePage>(Feed(cursor, limit: 20)))!;
            seen.AddRange(page.Items.Select(expense => expense.Id));
            cursor = page.NextCursor;
        }

        seen.Should().OnlyHaveUniqueItems();
        seen.Should().HaveCount(100);
    }

    [Fact]
    public async Task Feed_TieBreaksTwoExpensesOnTheSameDayById()
    {
        DateOnly sameDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        for (int index = 0; index < 6; index++)
        {
            await AddAsync(string.Create(CultureInfo.InvariantCulture, $"Gleicher Tag {index}"), sameDay);
        }

        List<Guid> seen = [];
        string? cursor = null;

        do
        {
            ExpensePage page = (await _anna.GetAsync<ExpensePage>(Feed(cursor, limit: 2)))!;
            seen.AddRange(page.Items.Select(expense => expense.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        // Without the id in the ordering, six rows sharing one date would page unpredictably.
        seen.Should().HaveCount(6);
        seen.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Cursor_IsOpaque()
    {
        await SeedAsync(3);

        ExpensePage page = (await _anna.GetAsync<ExpensePage>(Feed(limit: 1)))!;

        page.NextCursor.Should().NotBeNull();
        // No date, no id: a client that cannot read it cannot start building its own and freeze
        // the shape of the ordering.
        page.NextCursor.Should().NotContain("-").And.NotContain(page.Items[0].Id.ToString());
    }

    [Fact]
    public async Task Feed_WithAnUnreadableCursor_StartsFromTheBeginning()
    {
        await SeedAsync(3);

        // A stale bookmark gets the first page rather than an error.
        ExpensePage page = (await _anna.GetAsync<ExpensePage>(Feed("nicht-lesbar")))!;

        page.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Feed_CapsTheRequestedPageSize()
    {
        await SeedAsync(3);

        ExpensePage page = (await _anna.GetAsync<ExpensePage>(Feed(limit: 10_000)))!;

        page.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Feed_ForSomebodyElsesGroup_Answers404()
    {
        (HttpClient boris, _) = await SignInAsync("boris@example.com");

        HttpResponseMessage response = await boris.GetAsync(Feed());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Feed_IssuesTheSameNumberOfQueriesWhateverThePageHolds()
    {
        // Counted against the database rather than inferred from the LINQ, because the failure this
        // guards against is invisible in the source: a projection EF cannot translate falls back to
        // client evaluation and issues one query per row to fetch the member names.
        //
        // The assertion is that the count does not grow with the rows, not that it equals some
        // constant — a fixed number would also have to encode the membership check that precedes
        // the page, and would break the moment that check moved.
        await SeedAsync(4);
        int forFour = await CountQueriesAsync(() => _anna.GetAsync<ExpensePage>(Feed(limit: 50)));

        await SeedAsync(40);
        int forFortyFour = await CountQueriesAsync(() => _anna.GetAsync<ExpensePage>(Feed(limit: 50)));

        forFortyFour.Should().Be(forFour);
    }

    private static async Task<int> CountQueriesAsync(Func<Task<ExpensePage?>> read)
    {
        int queries = 0;

        using (CommandCounter.Start(_ => Interlocked.Increment(ref queries)))
        {
            ExpensePage? page = await read();
            page.Should().NotBeNull();
        }

        return queries;
    }
}
