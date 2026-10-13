using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Contracts;
using Geteilt.Contracts.Groups;
using Geteilt.Domain.Expenses;
using Geteilt.Domain.Members;
using Geteilt.Domain.Settlements;
using Geteilt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Geteilt.Api.Tests;

/// <summary>Groups over HTTP — the authorisation rules live here.</summary>
[Collection(SharedApiFixture.Name)]
public sealed class GroupEndpointTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private static readonly Uri Groups = new("/api/groups", UriKind.Relative);

    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> Use<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        return await work(scope.ServiceProvider);
    }

    /// <summary>Registers somebody and hands back a client already carrying their token.</summary>
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

    private static async Task<GroupDto> CreateAsync(HttpClient client, string name = "Italien 2026")
    {
        HttpResponseMessage response = await client.PostAsync(
            Groups, new CreateGroupRequest(name, "EUR"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadAsync<GroupDto>())!;
    }

    private static Uri Group(Guid id) => new($"/api/groups/{id}", UriKind.Relative);

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadAsync<Dictionary<string, object>>())
            ?.GetValueOrDefault("code")?.ToString();

    [Fact]
    public async Task Create_MakesTheCreatorAnOwnerMember()
    {
        (HttpClient client, Guid userId) = await SignInAsync("anna@example.com");

        GroupDto group = await CreateAsync(client);

        group.MemberCount.Should().Be(1);
        group.MyBalance.Should().Be(0m);
        group.BaseCurrency.Should().Be("EUR");

        Member owner = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Members.SingleAsync(member => member.GroupId == group.Id));

        owner.UserId.Should().Be(userId);
        owner.Role.Should().Be(MemberRole.Owner);
    }

    [Fact]
    public async Task Create_WithAnUnsupportedCurrency_Answers422()
    {
        (HttpClient client, _) = await SignInAsync("anna@example.com");

        HttpResponseMessage response = await client.PostAsync(
            Groups, new CreateGroupRequest("Nirgendwo", "XYZ"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("invalid_currency");
    }

    [Fact]
    public async Task List_ShowsOnlyTheCallersOwnGroups()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (HttpClient boris, _) = await SignInAsync("boris@example.com");

        await CreateAsync(anna, "Annas Reise");
        await CreateAsync(boris, "Boris' Reise");

        IReadOnlyList<GroupDto>? annasGroups =
            await anna.GetAsync<IReadOnlyList<GroupDto>>(Groups);

        annasGroups.Should().ContainSingle().Which.Name.Should().Be("Annas Reise");
    }

    [Fact]
    public async Task Get_ForSomebodyElsesGroup_Answers404AndNot403()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (HttpClient boris, _) = await SignInAsync("boris@example.com");

        GroupDto annas = await CreateAsync(anna);

        HttpResponseMessage response = await boris.GetAsync(Group(annas.Id));

        // 403 would confirm the group exists, which is itself a leak.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ForAGroupThatDoesNotExist_AnswersTheSameAsSomebodyElsesGroup()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (HttpClient boris, _) = await SignInAsync("boris@example.com");

        GroupDto annas = await CreateAsync(anna);

        HttpResponseMessage foreign = await boris.GetAsync(Group(annas.Id));
        HttpResponseMessage missing = await boris.GetAsync(Group(Guid.CreateVersion7()));

        missing.StatusCode.Should().Be(foreign.StatusCode);
    }

    [Fact]
    public async Task Patch_ForSomebodyElsesGroup_Answers404()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (HttpClient boris, _) = await SignInAsync("boris@example.com");

        GroupDto annas = await CreateAsync(anna);

        HttpResponseMessage response = await boris.PatchAsync(
            Group(annas.Id), new UpdateGroupRequest("Gekapert", null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_RenamesTheGroup()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateAsync(anna);

        HttpResponseMessage response = await anna.PatchAsync(
            Group(group.Id), new UpdateGroupRequest("Italien 2027", null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsync<GroupDto>())!.Name.Should().Be("Italien 2027");
    }

    [Fact]
    public async Task Patch_ByAPlainMember_Answers403()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (HttpClient boris, Guid borisId) = await SignInAsync("boris@example.com");

        GroupDto group = await CreateAsync(anna);
        await JoinAsync(group.Id, borisId, "Boris", MemberRole.Member);

        HttpResponseMessage response = await boris.PatchAsync(
            Group(group.Id), new UpdateGroupRequest("Boris war hier", null));

        // Boris can see this group, so its existence is no secret from him — he just lacks the role.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOf(response)).Should().Be("insufficient_role");
    }

    [Fact]
    public async Task Patch_CannotChangeTheBaseCurrency()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateAsync(anna);

        // The field does not exist on the request at all, so sending it changes nothing: the
        // currency is fixed at creation because rewriting history at historical rates has no
        // single right answer.
        HttpResponseMessage response = await anna.PatchAsync(
            Group(group.Id),
            JsonContent.Create(new { name = "Immer noch Italien", baseCurrency = "USD" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsync<GroupDto>())!.BaseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Patch_ArchivesAndUnarchives()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateAsync(anna);

        HttpResponseMessage archived = await anna.PatchAsync(
            Group(group.Id), new UpdateGroupRequest(null, true));
        (await archived.Content.ReadAsync<GroupDto>())!.ArchivedAt.Should().NotBeNull();

        HttpResponseMessage restored = await anna.PatchAsync(
            Group(group.Id), new UpdateGroupRequest(null, false));
        (await restored.Content.ReadAsync<GroupDto>())!.ArchivedAt.Should().BeNull();
    }

    [Fact]
    public async Task Patch_RenamingAnArchivedGroup_Answers409()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateAsync(anna);

        await anna.PatchAsync(Group(group.Id), new UpdateGroupRequest(null, true));

        HttpResponseMessage response = await anna.PatchAsync(
            Group(group.Id), new UpdateGroupRequest("Neuer Name", null));

        // A conflict, not a validation failure: the same request would have worked yesterday.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(response)).Should().Be("group_archived");
    }

    [Fact]
    public async Task List_ReportsWhatTheCallerIsOwed()
    {
        (HttpClient anna, Guid annaId) = await SignInAsync("anna@example.com");
        (_, Guid borisId) = await SignInAsync("boris@example.com");

        GroupDto group = await CreateAsync(anna);
        Guid borisMemberId = await JoinAsync(group.Id, borisId, "Boris", MemberRole.Member);
        Guid annaMemberId = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Members.Where(member => member.GroupId == group.Id && member.UserId == annaId)
            .Select(member => member.Id).SingleAsync());

        // Anna pays 60, split evenly with Boris: she is owed 30.
        await AddExpenseAsync(group.Id, annaMemberId, borisMemberId, 60.00m);

        IReadOnlyList<GroupDto>? groups = await anna.GetAsync<IReadOnlyList<GroupDto>>(Groups);

        groups.Should().ContainSingle().Which.MyBalance.Should().Be(30.00m);
    }

    [Fact]
    public async Task List_CountsASettlementAsRepayment()
    {
        (HttpClient anna, Guid annaId) = await SignInAsync("anna@example.com");
        (_, Guid borisId) = await SignInAsync("boris@example.com");

        GroupDto group = await CreateAsync(anna);
        Guid borisMemberId = await JoinAsync(group.Id, borisId, "Boris", MemberRole.Member);
        Guid annaMemberId = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Members.Where(member => member.GroupId == group.Id && member.UserId == annaId)
            .Select(member => member.Id).SingleAsync());

        await AddExpenseAsync(group.Id, annaMemberId, borisMemberId, 60.00m);

        // Boris pays Anna back the 30 he owes. Both balances must land on zero — with the
        // settlement signs the other way round Anna would end up at +60.
        await Use(async provider =>
        {
            AppDbContext db = provider.GetRequiredService<AppDbContext>();

            db.Settlements.Add(Settlement.Create(
                Guid.CreateVersion7(),
                group.Id,
                fromMemberId: borisMemberId,
                toMemberId: annaMemberId,
                amount: 30.00m,
                currency: "EUR",
                date: DateOnly.FromDateTime(DateTime.UtcNow),
                createdAt: DateTimeOffset.UtcNow,
                method: SettlementMethod.Cash));

            return await db.SaveChangesAsync(CancellationToken.None);
        });

        IReadOnlyList<GroupDto>? groups = await anna.GetAsync<IReadOnlyList<GroupDto>>(Groups);

        groups.Should().ContainSingle().Which.MyBalance.Should().Be(0m);
    }

    [Fact]
    public async Task List_WithoutAToken_Answers401()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync(Groups);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<Guid> JoinAsync(Guid groupId, Guid userId, string displayName, MemberRole role) =>
        Use(async provider =>
        {
            AppDbContext db = provider.GetRequiredService<AppDbContext>();
            Member member = new(Guid.CreateVersion7(), groupId, displayName, DateTimeOffset.UtcNow, userId, role);

            db.Members.Add(member);
            await db.SaveChangesAsync(CancellationToken.None);

            return member.Id;
        });

    private Task<int> AddExpenseAsync(Guid groupId, Guid paidBy, Guid otherMember, decimal amount) =>
        Use(async provider =>
        {
            AppDbContext db = provider.GetRequiredService<AppDbContext>();
            decimal half = amount / 2;

            Guid expenseId = Guid.CreateVersion7();

            Expense expense = Expense.Create(
                new NewExpense
                {
                    Id = expenseId,
                    GroupId = groupId,
                    Description = "Abendessen",
                    PaidByMemberId = paidBy,
                    Amount = amount,
                    Currency = "EUR",
                    Date = DateOnly.FromDateTime(DateTime.UtcNow),
                    SplitMethod = SplitMethod.Equal,
                    CreatedByMemberId = paidBy,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                [
                    new ExpenseShare(expenseId, paidBy, half, half),
                    new ExpenseShare(expenseId, otherMember, half, half),
                ],
                groupMemberIds: [paidBy, otherMember],
                groupBaseCurrency: "EUR");

            db.Expenses.Add(expense);

            return await db.SaveChangesAsync(CancellationToken.None);
        });
}
