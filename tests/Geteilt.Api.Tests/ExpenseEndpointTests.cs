using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

/// <summary>
/// Creating an expense — the point where the money rules meet HTTP.
/// </summary>
[Collection(SharedApiFixture.Name)]
public sealed class ExpenseEndpointTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private HttpClient _anna = null!;
    private GroupDto _group = null!;
    private Guid _annaMemberId;
    private Guid _borisMemberId;
    private Guid _claraMemberId;

    public async Task InitializeAsync()
    {
        await factory.ResetAsync();

        (_anna, _) = await SignInAsync("anna@example.com");
        _group = await CreateGroupAsync(_anna);

        MemberDto boris = await AddMemberAsync("Boris");
        MemberDto clara = await AddMemberAsync("Clara");

        _borisMemberId = boris.Id;
        _claraMemberId = clara.Id;

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

    private static async Task<GroupDto> CreateGroupAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsync(
            new Uri("/api/groups", UriKind.Relative), new CreateGroupRequest("Italien 2026", "EUR"));

        return (await response.Content.ReadAsync<GroupDto>())!;
    }

    private async Task<MemberDto> AddMemberAsync(string name)
    {
        HttpResponseMessage response = await _anna.PostAsync(
            new Uri($"/api/groups/{_group.Id}/members", UriKind.Relative),
            new AddMemberRequest(name, null, null));

        return (await response.Content.ReadAsync<MemberDto>())!;
    }

    private Uri Expenses => new($"/api/groups/{_group.Id}/expenses", UriKind.Relative);

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadAsync<Dictionary<string, object>>())
            ?.GetValueOrDefault("code")?.ToString();

    private CreateExpenseRequest EvenSplit(decimal amount, params Guid[] members) => new(
        "Abendessen",
        _annaMemberId,
        amount,
        "EUR",
        DateOnly.FromDateTime(DateTime.UtcNow),
        SplitMethod.Equal,
        [.. members.Select(id => new ShareInputDto(id, null, null))]);

    [Fact]
    public async Task Create_SplittingTenOverThree_Stores334And333And333()
    {
        // From the equal-split reference table. The extra cent goes to the lowest member id, and it has
        // to be the same cent every time the expense is read back.
        HttpResponseMessage response = await _anna.PostAsync(
            Expenses, EvenSplit(10.00m, _annaMemberId, _borisMemberId, _claraMemberId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        ExpenseDto expense = (await response.Content.ReadAsync<ExpenseDto>())!;

        expense.Shares.Sum(share => share.ShareAmount).Should().Be(10.00m);
        expense.Shares.Select(share => share.ShareAmount)
            .OrderByDescending(amount => amount)
            .Should().Equal(3.34m, 3.33m, 3.33m);

        Guid lowest = new[] { _annaMemberId, _borisMemberId, _claraMemberId }
            .OrderBy(id => id.ToString(), StringComparer.Ordinal).First();

        expense.Shares.Single(share => share.MemberId == lowest).ShareAmount.Should().Be(3.34m);
    }

    [Fact]
    public async Task Create_ReturnsCreatedWithALocationHeader()
    {
        HttpResponseMessage response = await _anna.PostAsync(
            Expenses, EvenSplit(10.00m, _annaMemberId, _borisMemberId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        HttpResponseMessage fetched = await _anna.GetAsync(response.Headers.Location);
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_FillsTheBaseCurrencyAmountAndRate()
    {
        HttpResponseMessage response = await _anna.PostAsync(
            Expenses, EvenSplit(30.00m, _annaMemberId, _borisMemberId));

        ExpenseDto expense = (await response.Content.ReadAsync<ExpenseDto>())!;

        expense.AmountInBaseCurrency.Should().Be(30.00m);
        expense.ExchangeRate.Should().Be(1.000000m);
    }

    [Fact]
    public async Task Create_WithShares_DividesByWeight()
    {
        HttpResponseMessage response = await _anna.PostAsync(Expenses, new CreateExpenseRequest(
            "Ferienhaus",
            _annaMemberId,
            100.00m,
            "EUR",
            DateOnly.FromDateTime(DateTime.UtcNow),
            SplitMethod.Shares,
            [
                new ShareInputDto(_annaMemberId, null, 2m),
                new ShareInputDto(_borisMemberId, null, 1m),
                new ShareInputDto(_claraMemberId, null, 1m),
            ]));

        ExpenseDto expense = (await response.Content.ReadAsync<ExpenseDto>())!;

        expense.Shares.Single(share => share.MemberId == _annaMemberId).ShareAmount.Should().Be(50.00m);
        expense.Shares.Where(share => share.MemberId != _annaMemberId)
            .Should().OnlyContain(share => share.ShareAmount == 25.00m);
    }

    [Fact]
    public async Task Create_WithExactAmountsThatDoNotAddUp_Answers422()
    {
        HttpResponseMessage response = await _anna.PostAsync(Expenses, new CreateExpenseRequest(
            "Abendessen",
            _annaMemberId,
            100.00m,
            "EUR",
            DateOnly.FromDateTime(DateTime.UtcNow),
            SplitMethod.ExactAmounts,
            [
                new ShareInputDto(_annaMemberId, 50.00m, null),
                new ShareInputDto(_borisMemberId, 49.99m, null),
            ]));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("shares_sum_mismatch");
    }

    [Fact]
    public async Task Create_WithPercentagesThatDoNotMakeAHundred_Answers422()
    {
        HttpResponseMessage response = await _anna.PostAsync(Expenses, new CreateExpenseRequest(
            "Abendessen",
            _annaMemberId,
            100.00m,
            "EUR",
            DateOnly.FromDateTime(DateTime.UtcNow),
            SplitMethod.Percentages,
            [
                new ShareInputDto(_annaMemberId, null, 60.00m),
                new ShareInputDto(_borisMemberId, null, 30.00m),
            ]));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("percentages_sum_mismatch");
    }

    [Fact]
    public async Task Create_WithAPayerFromAnotherGroup_Answers422()
    {
        (HttpClient boris, _) = await SignInAsync("boris@example.com");
        GroupDto other = await CreateGroupAsync(boris);

        Guid outsider = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Members.Where(member => member.GroupId == other.Id)
            .Select(member => member.Id).SingleAsync());

        HttpResponseMessage response = await _anna.PostAsync(
            Expenses,
            EvenSplit(10.00m, _annaMemberId, _borisMemberId) with { PaidByMemberId = outsider });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("member_not_in_group");
    }

    [Fact]
    public async Task Create_InACurrencyOtherThanTheGroups_Answers422()
    {
        HttpResponseMessage response = await _anna.PostAsync(
            Expenses,
            EvenSplit(10.00m, _annaMemberId, _borisMemberId) with { Currency = "CHF" });

        // An expense is always in the group's own currency; anything else would need a
        // historical rate we do not have.
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("currency_mismatch");
    }

    [Fact]
    public async Task Create_DatedMoreThanADayAhead_Answers422()
    {
        HttpResponseMessage response = await _anna.PostAsync(
            Expenses,
            EvenSplit(10.00m, _annaMemberId, _borisMemberId) with
            {
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("date_in_future");
    }

    [Fact]
    public async Task Create_DatedTomorrow_IsAccepted()
    {
        // One day of slack, because time zones mean "today" is not the same everywhere.
        HttpResponseMessage response = await _anna.PostAsync(
            Expenses,
            EvenSplit(10.00m, _annaMemberId, _borisMemberId) with
            {
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithANonPositiveAmount_Answers422()
    {
        HttpResponseMessage response = await _anna.PostAsync(
            Expenses, EvenSplit(0m, _annaMemberId, _borisMemberId));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("amount_must_be_positive");
    }

    [Fact]
    public async Task Create_InSomebodyElsesGroup_Answers404()
    {
        (HttpClient boris, _) = await SignInAsync("boris@example.com");

        HttpResponseMessage response = await boris.PostAsync(
            Expenses, EvenSplit(10.00m, _annaMemberId, _borisMemberId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WritesTheExpenseAndItsSharesTogether()
    {
        await _anna.PostAsync(Expenses, EvenSplit(10.00m, _annaMemberId, _borisMemberId, _claraMemberId));

        (int expenses, int shares) = await Use(async provider =>
        {
            AppDbContext db = provider.GetRequiredService<AppDbContext>();

            return (
                await db.Expenses.CountAsync(),
                await db.Expenses.SelectMany(expense => expense.Shares).CountAsync());
        });

        expenses.Should().Be(1);
        shares.Should().Be(3);
    }

    [Fact]
    public async Task Create_WhenTheSplitIsRejected_WritesNothing()
    {
        await _anna.PostAsync(Expenses, new CreateExpenseRequest(
            "Abendessen",
            _annaMemberId,
            100.00m,
            "EUR",
            DateOnly.FromDateTime(DateTime.UtcNow),
            SplitMethod.ExactAmounts,
            [new ShareInputDto(_annaMemberId, 99.99m, null)]));

        int expenses = await Use(provider =>
            provider.GetRequiredService<AppDbContext>().Expenses.CountAsync());

        expenses.Should().Be(0);
    }

    [Fact]
    public async Task Create_ReadBackTwice_KeepsTheSameCentOnTheSamePerson()
    {
        HttpResponseMessage created = await _anna.PostAsync(
            Expenses, EvenSplit(10.00m, _annaMemberId, _borisMemberId, _claraMemberId));

        Uri location = created.Headers.Location!;

        ExpenseDto first = (await (await _anna.GetAsync(location)).Content.ReadAsync<ExpenseDto>())!;
        ExpenseDto second = (await (await _anna.GetAsync(location)).Content.ReadAsync<ExpenseDto>())!;

        // Shares are stored, not recomputed on read: whoever got the extra cent keeps it, even if
        // somebody joins the group tomorrow.
        second.Shares.OrderBy(share => share.MemberId)
            .Should().BeEquivalentTo(first.Shares.OrderBy(share => share.MemberId));
    }

    [Fact]
    public async Task Get_ForSomebodyElsesGroup_Answers404()
    {
        HttpResponseMessage created = await _anna.PostAsync(
            Expenses, EvenSplit(10.00m, _annaMemberId, _borisMemberId));

        (HttpClient boris, _) = await SignInAsync("boris@example.com");

        HttpResponseMessage response = await boris.GetAsync(created.Headers.Location);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
