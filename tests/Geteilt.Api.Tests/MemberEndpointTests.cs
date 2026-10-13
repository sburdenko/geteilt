using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Contracts;
using Geteilt.Contracts.Groups;
using Geteilt.Contracts.Members;
using Geteilt.Domain.Expenses;
using Geteilt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MemberRole = Geteilt.Contracts.Members.MemberRole;

namespace Geteilt.Api.Tests;

/// <summary>Members with and without an account of their own.</summary>
[Collection(SharedApiFixture.Name)]
public sealed class MemberEndpointTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetAsync();

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

    private static Uri Members(Guid groupId) => new($"/api/groups/{groupId}/members", UriKind.Relative);

    private static Uri Member(Guid groupId, Guid memberId) =>
        new($"/api/groups/{groupId}/members/{memberId}", UriKind.Relative);

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadAsync<Dictionary<string, object>>())
            ?.GetValueOrDefault("code")?.ToString();

    private static async Task<MemberDto> AddAsync(HttpClient client, Guid groupId, AddMemberRequest request)
    {
        HttpResponseMessage response = await client.PostAsync(Members(groupId), request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadAsync<MemberDto>())!;
    }

    [Fact]
    public async Task Add_WithoutAnEmail_CreatesSomebodyWithoutAnAccount()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        // The friend who never installs anything still has to count in every balance.
        MemberDto member = await AddAsync(anna, group.Id, new AddMemberRequest("Clara", null, null));

        member.UserId.Should().BeNull();
        member.HasAccount.Should().BeFalse();
        member.Role.Should().Be(MemberRole.Member);
        member.RemovedAt.Should().BeNull();
    }

    [Fact]
    public async Task Add_WithAKnownEmail_LinksThatAccount()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (_, Guid borisId) = await SignInAsync("boris@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        MemberDto member = await AddAsync(
            anna, group.Id, new AddMemberRequest("Boris", "BORIS@example.com", null));

        member.UserId.Should().Be(borisId);
        member.HasAccount.Should().BeTrue();
    }

    [Fact]
    public async Task Add_WithAnUnknownEmail_StillJoinsWithoutAnAccount()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        // Not an error: they can be linked later if they ever sign up.
        MemberDto member = await AddAsync(
            anna, group.Id, new AddMemberRequest("Clara", "clara@example.com", null));

        member.UserId.Should().BeNull();
    }

    [Fact]
    public async Task Add_TheSameAccountTwice_Answers409()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        await SignInAsync("boris@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        await AddAsync(anna, group.Id, new AddMemberRequest("Boris", "boris@example.com", null));

        HttpResponseMessage response = await anna.PostAsync(
            Members(group.Id), new AddMemberRequest("Boris nochmal", "boris@example.com", null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(response)).Should().Be("member_already_in_group");
    }

    [Fact]
    public async Task Add_NormalisesTheIban()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        MemberDto member = await AddAsync(
            anna, group.Id, new AddMemberRequest("Clara", null, "de89 3704 0044 0532 0130 00"));

        member.Iban.Should().Be("DE89370400440532013000");
    }

    [Fact]
    public async Task Add_WithAnInvalidIban_Answers422()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        HttpResponseMessage response = await anna.PostAsync(
            Members(group.Id), new AddMemberRequest("Clara", null, "DE89370400440532013001"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("invalid_iban");
    }

    [Fact]
    public async Task Add_ByAPlainMember_Answers403()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (HttpClient boris, Guid borisId) = await SignInAsync("boris@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        await AddAsync(anna, group.Id, new AddMemberRequest("Boris", "boris@example.com", null));
        _ = borisId;

        HttpResponseMessage response = await boris.PostAsync(
            Members(group.Id), new AddMemberRequest("Clara", null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CodeOf(response)).Should().Be("insufficient_role");
    }

    [Fact]
    public async Task Add_ToSomebodyElsesGroup_Answers404()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (HttpClient boris, _) = await SignInAsync("boris@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        HttpResponseMessage response = await boris.PostAsync(
            Members(group.Id), new AddMemberRequest("Clara", null, null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_BeyondTwentyMembers_Answers422()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        // The owner already occupies the first slot, so nineteen more fill the group.
        for (int index = 0; index < 19; index++)
        {
            await AddAsync(
                anna,
                group.Id,
                new AddMemberRequest(string.Create(CultureInfo.InvariantCulture, $"Gast {index}"), null, null));
        }

        HttpResponseMessage response = await anna.PostAsync(
            Members(group.Id), new AddMemberRequest("Einer zu viel", null, null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("group_member_limit");
    }

    [Fact]
    public async Task Add_ToAnArchivedGroup_Answers409()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        await anna.PatchAsync(
            new Uri($"/api/groups/{group.Id}", UriKind.Relative), new UpdateGroupRequest(null, true));

        HttpResponseMessage response = await anna.PostAsync(
            Members(group.Id), new AddMemberRequest("Clara", null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(response)).Should().Be("group_archived");
    }

    [Fact]
    public async Task Remove_SomebodyWithNothingBehindThem_Answers204()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateGroupAsync(anna);
        MemberDto clara = await AddAsync(anna, group.Id, new AddMemberRequest("Clara", null, null));

        HttpResponseMessage response = await anna.DeleteAsync(Member(group.Id, clara.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        IReadOnlyList<MemberDto>? remaining =
            await anna.GetAsync<IReadOnlyList<MemberDto>>(Members(group.Id));
        remaining.Should().ContainSingle();
    }

    [Fact]
    public async Task Remove_SomebodyWithExpenses_Answers409()
    {
        (HttpClient anna, Guid annaId) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateGroupAsync(anna);
        MemberDto clara = await AddAsync(anna, group.Id, new AddMemberRequest("Clara", null, null));

        Guid annaMemberId = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Members.Where(member => member.GroupId == group.Id && member.UserId == annaId)
            .Select(member => member.Id).SingleAsync());

        await AddExpenseAsync(group.Id, annaMemberId, clara.Id);

        HttpResponseMessage response = await anna.DeleteAsync(Member(group.Id, clara.Id));

        // Deleting the row would drop their share out of the sums, and "all balances add up to
        // zero" would stop holding for a group that was fine a second ago.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(response)).Should().Be("member_has_activity");
    }

    [Fact]
    public async Task Remove_ByAPlainMember_Answers403()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (HttpClient boris, _) = await SignInAsync("boris@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        await AddAsync(anna, group.Id, new AddMemberRequest("Boris", "boris@example.com", null));
        MemberDto clara = await AddAsync(anna, group.Id, new AddMemberRequest("Clara", null, null));

        HttpResponseMessage response = await boris.DeleteAsync(Member(group.Id, clara.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Patch_ChangesTheDisplayNameAndIban()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        GroupDto group = await CreateGroupAsync(anna);
        MemberDto clara = await AddAsync(anna, group.Id, new AddMemberRequest("Clara", null, null));

        HttpResponseMessage response = await anna.PatchAsync(
            Member(group.Id, clara.Id),
            new UpdateMemberRequest("Clara Weber", "DE89370400440532013000", null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        MemberDto updated = (await response.Content.ReadAsync<MemberDto>())!;
        updated.DisplayName.Should().Be("Clara Weber");
        updated.Iban.Should().Be("DE89370400440532013000");
    }

    [Fact]
    public async Task Patch_ChangingARole_IsForTheOwnerOnly()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (HttpClient boris, _) = await SignInAsync("boris@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        MemberDto borisMember = await AddAsync(
            anna, group.Id, new AddMemberRequest("Boris", "boris@example.com", null));

        HttpResponseMessage promotesHimself = await boris.PatchAsync(
            Member(group.Id, borisMember.Id), new UpdateMemberRequest(null, null, MemberRole.Owner));

        promotesHimself.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage promotedByOwner = await anna.PatchAsync(
            Member(group.Id, borisMember.Id), new UpdateMemberRequest(null, null, MemberRole.Owner));

        promotedByOwner.StatusCode.Should().Be(HttpStatusCode.OK);
        (await promotedByOwner.Content.ReadAsync<MemberDto>())!.Role.Should().Be(MemberRole.Owner);
    }

    [Fact]
    public async Task Patch_EditingOwnDisplayName_DoesNotNeedTheOwnerRole()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (HttpClient boris, _) = await SignInAsync("boris@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        MemberDto borisMember = await AddAsync(
            anna, group.Id, new AddMemberRequest("Boris", "boris@example.com", null));

        HttpResponseMessage response = await boris.PatchAsync(
            Member(group.Id, borisMember.Id), new UpdateMemberRequest("Boris B.", null, null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_ForSomebodyElsesGroup_Answers404()
    {
        (HttpClient anna, _) = await SignInAsync("anna@example.com");
        (HttpClient boris, _) = await SignInAsync("boris@example.com");
        GroupDto group = await CreateGroupAsync(anna);

        HttpResponseMessage response = await boris.GetAsync(Members(group.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private Task<int> AddExpenseAsync(Guid groupId, Guid paidBy, Guid otherMember) =>
        Use(async provider =>
        {
            AppDbContext db = provider.GetRequiredService<AppDbContext>();
            Guid expenseId = Guid.CreateVersion7();

            db.Expenses.Add(Expense.Create(
                new NewExpense
                {
                    Id = expenseId,
                    GroupId = groupId,
                    Description = "Abendessen",
                    PaidByMemberId = paidBy,
                    Amount = 10.00m,
                    Currency = "EUR",
                    Date = DateOnly.FromDateTime(DateTime.UtcNow),
                    SplitMethod = SplitMethod.Equal,
                    CreatedByMemberId = paidBy,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                [
                    new ExpenseShare(expenseId, paidBy, 5.00m, 5.00m),
                    new ExpenseShare(expenseId, otherMember, 5.00m, 5.00m),
                ],
                groupMemberIds: [paidBy, otherMember],
                groupBaseCurrency: "EUR"));

            return await db.SaveChangesAsync(CancellationToken.None);
        });
}
