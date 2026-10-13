using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Geteilt.Api.Idempotency;
using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Contracts;
using Geteilt.Contracts.Balances;
using Geteilt.Contracts.Expenses;
using Geteilt.Contracts.Groups;
using Geteilt.Contracts.Members;
using Geteilt.Contracts.Settlements;
using Geteilt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Geteilt.Api.Tests;

/// <summary>Recording that money actually changed hands.</summary>
[Collection(SharedApiFixture.Name)]
public sealed class SettlementEndpointTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private HttpClient _anna = null!;
    private GroupDto _group = null!;
    private Guid _annaMember;
    private Guid _boris;
    private Guid _clara;

    public async Task InitializeAsync()
    {
        await factory.ResetAsync();

        (_anna, _) = await SignInAsync("anna@example.com");

        HttpResponseMessage created = await _anna.PostAsync(
            new Uri("/api/groups", UriKind.Relative), new CreateGroupRequest("Italien 2026", "EUR"));
        _group = (await created.Content.ReadAsync<GroupDto>())!;

        _boris = (await AddMemberAsync("Boris")).Id;
        _clara = (await AddMemberAsync("Clara")).Id;

        _annaMember = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Members.Where(member => member.GroupId == _group.Id && member.UserId != null)
            .Select(member => member.Id).SingleAsync());

        // The dinner-and-taxi example again: Anna is owed 30, Clara owes 30.
        await _anna.PostAsync(
            new Uri($"/api/groups/{_group.Id}/expenses", UriKind.Relative),
            new CreateExpenseRequest(
                "Abendessen", _annaMember, 60.00m, "EUR", DateOnly.FromDateTime(DateTime.UtcNow),
                SplitMethod.Equal,
                [
                    new ShareInputDto(_annaMember, null, null),
                    new ShareInputDto(_boris, null, null),
                    new ShareInputDto(_clara, null, null),
                ]));

        await _anna.PostAsync(
            new Uri($"/api/groups/{_group.Id}/expenses", UriKind.Relative),
            new CreateExpenseRequest(
                "Taxi", _boris, 30.00m, "EUR", DateOnly.FromDateTime(DateTime.UtcNow),
                SplitMethod.Equal,
                [
                    new ShareInputDto(_annaMember, null, null),
                    new ShareInputDto(_boris, null, null),
                    new ShareInputDto(_clara, null, null),
                ]));
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

    private async Task<MemberDto> AddMemberAsync(string name)
    {
        HttpResponseMessage response = await _anna.PostAsync(
            new Uri($"/api/groups/{_group.Id}/members", UriKind.Relative),
            new AddMemberRequest(name, null, null));

        return (await response.Content.ReadAsync<MemberDto>())!;
    }

    private Uri Settlements => new($"/api/groups/{_group.Id}/settlements", UriKind.Relative);

    private Uri Settlement(Guid id) => new($"/api/groups/{_group.Id}/settlements/{id}", UriKind.Relative);

    private Uri Balances => new($"/api/groups/{_group.Id}/balances", UriKind.Relative);

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadAsync<Dictionary<string, object>>())
            ?.GetValueOrDefault("code")?.ToString();

    private static CreateSettlementRequest Repayment(Guid from, Guid to, decimal amount) => new(
        from, to, amount, "EUR", DateOnly.FromDateTime(DateTime.UtcNow), SettlementMethod.BankTransfer, null);

    private async Task<decimal> BalanceOfAsync(Guid memberId)
    {
        BalancesResponse response = (await _anna.GetAsync<BalancesResponse>(Balances))!;

        return response.Balances.Single(balance => balance.MemberId == memberId).Balance;
    }

    [Fact]
    public async Task Post_MovesBothBalancesToZero()
    {
        (await BalanceOfAsync(_annaMember)).Should().Be(30.00m);
        (await BalanceOfAsync(_clara)).Should().Be(-30.00m);

        HttpResponseMessage response = await _anna.PostAsync(
            Settlements, Repayment(_clara, _annaMember, 30.00m));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // The sender gets a plus and the receiver a minus. The other way round this would have
        // deepened the very debt it repays.
        (await BalanceOfAsync(_annaMember)).Should().Be(0m);
        (await BalanceOfAsync(_clara)).Should().Be(0m);
    }

    [Fact]
    public async Task Post_ClearsTheSettlementPlan()
    {
        await _anna.PostAsync(Settlements, Repayment(_clara, _annaMember, 30.00m));

        SettlementPlanResponse plan = (await _anna.GetAsync<SettlementPlanResponse>(
            new Uri($"/api/groups/{_group.Id}/settlement-plan", UriKind.Relative)))!;

        plan.Transfers.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_ToOneself_Answers422()
    {
        HttpResponseMessage response = await _anna.PostAsync(
            Settlements, Repayment(_clara, _clara, 10.00m));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_WithAMemberFromAnotherGroup_Answers422()
    {
        (HttpClient boris, _) = await SignInAsync("boris@example.com");
        HttpResponseMessage other = await boris.PostAsync(
            new Uri("/api/groups", UriKind.Relative), new CreateGroupRequest("Andere", "EUR"));
        GroupDto otherGroup = (await other.Content.ReadAsync<GroupDto>())!;

        Guid outsider = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Members.Where(member => member.GroupId == otherGroup.Id)
            .Select(member => member.Id).SingleAsync());

        HttpResponseMessage response = await _anna.PostAsync(
            Settlements, Repayment(outsider, _annaMember, 10.00m));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("member_not_in_group");
    }

    [Fact]
    public async Task Post_WithANonPositiveAmount_Answers422()
    {
        HttpResponseMessage response = await _anna.PostAsync(
            Settlements, Repayment(_clara, _annaMember, 0m));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_RepeatedWithOneKey_RecordsASingleRepayment()
    {
        string key = Guid.CreateVersion7().ToString();

        for (int attempt = 0; attempt < 2; attempt++)
        {
            HttpRequestMessage request = new(HttpMethod.Post, Settlements)
            {
                Content = JsonContent.Create(Repayment(_clara, _annaMember, 30.00m), options: GeteiltJson.Options),
            };
            request.Headers.Add(IdempotencyFilter.HeaderName, key);

            await _anna.SendAsync(request);
        }

        // Without this the retry from a phone that lost its connection would repay twice, and Anna
        // would end up owing Clara thirty euro she never received.
        (await BalanceOfAsync(_annaMember)).Should().Be(0m);

        int stored = await Use(provider =>
            provider.GetRequiredService<AppDbContext>().Settlements.CountAsync());

        stored.Should().Be(1);
    }

    [Fact]
    public async Task Delete_PutsTheBalancesBack()
    {
        HttpResponseMessage created = await _anna.PostAsync(
            Settlements, Repayment(_clara, _annaMember, 30.00m));
        SettlementDto settlement = (await created.Content.ReadAsync<SettlementDto>())!;

        (await BalanceOfAsync(_annaMember)).Should().Be(0m);

        HttpResponseMessage deleted = await _anna.DeleteAsync(Settlement(settlement.Id));
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await BalanceOfAsync(_annaMember)).Should().Be(30.00m);

        int stored = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Settlements.IgnoreQueryFilters().CountAsync());

        stored.Should().Be(1);
    }

    [Fact]
    public async Task Delete_TwiceAnswersTheSameWay()
    {
        HttpResponseMessage created = await _anna.PostAsync(
            Settlements, Repayment(_clara, _annaMember, 30.00m));
        SettlementDto settlement = (await created.Content.ReadAsync<SettlementDto>())!;

        (await _anna.DeleteAsync(Settlement(settlement.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _anna.DeleteAsync(Settlement(settlement.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task List_ReturnsTheRepaymentsNewestFirst()
    {
        await _anna.PostAsync(Settlements, Repayment(_clara, _annaMember, 10.00m));
        await _anna.PostAsync(Settlements, Repayment(_boris, _annaMember, 5.00m));

        SettlementPage page = (await _anna.GetAsync<SettlementPage>(Settlements))!;

        page.Items.Should().HaveCount(2);
        page.Items.Should().OnlyContain(settlement => settlement.ToName == "anna");
    }

    [Fact]
    public async Task Post_InSomebodyElsesGroup_Answers404()
    {
        (HttpClient boris, _) = await SignInAsync("boris@example.com");

        HttpResponseMessage response = await boris.PostAsync(
            Settlements, Repayment(_clara, _annaMember, 10.00m));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
