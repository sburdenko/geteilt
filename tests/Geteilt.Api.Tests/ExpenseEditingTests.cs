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

/// <summary>Editing replaces everything, deleting keeps the row.</summary>
[Collection(SharedApiFixture.Name)]
public sealed class ExpenseEditingTests(GeteiltApiFactory factory) : IAsyncLifetime
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

        HttpResponseMessage created = await _anna.PostAsync(
            new Uri("/api/groups", UriKind.Relative), new CreateGroupRequest("Italien 2026", "EUR"));
        _group = (await created.Content.ReadAsync<GroupDto>())!;

        _borisMemberId = (await AddMemberAsync("Boris")).Id;
        _claraMemberId = (await AddMemberAsync("Clara")).Id;

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

    private async Task<MemberDto> AddMemberAsync(string name)
    {
        HttpResponseMessage response = await _anna.PostAsync(
            new Uri($"/api/groups/{_group.Id}/members", UriKind.Relative),
            new AddMemberRequest(name, null, null));

        return (await response.Content.ReadAsync<MemberDto>())!;
    }

    private Uri Expenses => new($"/api/groups/{_group.Id}/expenses", UriKind.Relative);

    private Uri Expense(Guid id) => new($"/api/groups/{_group.Id}/expenses/{id}", UriKind.Relative);

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

    private async Task<ExpenseDto> CreateAsync(decimal amount, params Guid[] members)
    {
        HttpResponseMessage response = await _anna.PostAsync(Expenses, EvenSplit(amount, members));

        return (await response.Content.ReadAsync<ExpenseDto>())!;
    }

    [Fact]
    public async Task Put_ReplacesTheSharesAndRemovesTheOldOnes()
    {
        ExpenseDto original = await CreateAsync(30.00m, _annaMemberId, _borisMemberId, _claraMemberId);
        original.Shares.Should().HaveCount(3);

        HttpResponseMessage response = await _anna.PutAsync(
            Expense(original.Id), EvenSplit(30.00m, _annaMemberId, _borisMemberId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        ExpenseDto updated = (await response.Content.ReadAsync<ExpenseDto>())!;
        updated.Shares.Should().HaveCount(2);
        updated.Shares.Should().OnlyContain(share => share.ShareAmount == 15.00m);
        updated.UpdatedAt.Should().NotBeNull();

        // Clara's row must be gone, not merely absent from the response.
        int rows = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Expenses.SelectMany(expense => expense.Shares).CountAsync());

        rows.Should().Be(2);
    }

    [Fact]
    public async Task Put_ChangesTheAmountAndRecomputesTheShares()
    {
        ExpenseDto original = await CreateAsync(10.00m, _annaMemberId, _borisMemberId, _claraMemberId);

        HttpResponseMessage response = await _anna.PutAsync(
            Expense(original.Id), EvenSplit(100.00m, _annaMemberId, _borisMemberId, _claraMemberId));

        ExpenseDto updated = (await response.Content.ReadAsync<ExpenseDto>())!;

        updated.Amount.Should().Be(100.00m);
        updated.AmountInBaseCurrency.Should().Be(100.00m);
        updated.Shares.Sum(share => share.ShareAmount).Should().Be(100.00m);
        updated.Shares.Select(share => share.ShareAmount)
            .OrderByDescending(amount => amount).Should().Equal(33.34m, 33.33m, 33.33m);
    }

    [Fact]
    public async Task Put_WithSharesThatDoNotAddUp_Answers422AndChangesNothing()
    {
        ExpenseDto original = await CreateAsync(30.00m, _annaMemberId, _borisMemberId);

        HttpResponseMessage response = await _anna.PutAsync(Expense(original.Id), new CreateExpenseRequest(
            "Abendessen",
            _annaMemberId,
            30.00m,
            "EUR",
            DateOnly.FromDateTime(DateTime.UtcNow),
            SplitMethod.ExactAmounts,
            [
                new ShareInputDto(_annaMemberId, 20.00m, null),
                new ShareInputDto(_borisMemberId, 5.00m, null),
            ]));

        // The invariant is checked on the way in for an edit exactly as for a creation: an edit
        // must not be able to reach a state a creation would have refused.
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("shares_sum_mismatch");

        ExpenseDto unchanged = (await _anna.GetAsync<ExpenseDto>(Expense(original.Id)))!;
        unchanged.Shares.Sum(share => share.ShareAmount).Should().Be(30.00m);
        unchanged.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Put_WithAMemberFromAnotherGroup_Answers422()
    {
        ExpenseDto original = await CreateAsync(30.00m, _annaMemberId, _borisMemberId);

        (HttpClient boris, _) = await SignInAsync("boris@example.com");
        HttpResponseMessage other = await boris.PostAsync(
            new Uri("/api/groups", UriKind.Relative), new CreateGroupRequest("Andere", "EUR"));
        GroupDto otherGroup = (await other.Content.ReadAsync<GroupDto>())!;

        Guid outsider = await Use(provider => provider.GetRequiredService<AppDbContext>()
            .Members.Where(member => member.GroupId == otherGroup.Id)
            .Select(member => member.Id).SingleAsync());

        HttpResponseMessage response = await _anna.PutAsync(
            Expense(original.Id), EvenSplit(30.00m, _annaMemberId, outsider));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeOf(response)).Should().Be("member_not_in_group");
    }

    [Fact]
    public async Task Delete_MarksTheRowAndKeepsIt()
    {
        ExpenseDto expense = await CreateAsync(30.00m, _annaMemberId, _borisMemberId);

        HttpResponseMessage response = await _anna.DeleteAsync(Expense(expense.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (int visible, int stored) = await Use(async provider =>
        {
            AppDbContext db = provider.GetRequiredService<AppDbContext>();

            return (
                await db.Expenses.CountAsync(),
                await db.Expenses.IgnoreQueryFilters().CountAsync());
        });

        // The row stays so the history stays consistent.
        visible.Should().Be(0);
        stored.Should().Be(1);
    }

    [Fact]
    public async Task Delete_TwiceAnswersTheSameWay()
    {
        ExpenseDto expense = await CreateAsync(30.00m, _annaMemberId, _borisMemberId);

        HttpResponseMessage first = await _anna.DeleteAsync(Expense(expense.Id));
        HttpResponseMessage second = await _anna.DeleteAsync(Expense(expense.Id));

        // DELETE says "make it not be there", and after the first call it already is not. A 404 on
        // the retry would make a lost response look like a failure.
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ForAnExpenseThatNeverExisted_Answers404()
    {
        HttpResponseMessage response = await _anna.DeleteAsync(Expense(Guid.CreateVersion7()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_TakesTheExpenseOutOfTheBalances()
    {
        ExpenseDto expense = await CreateAsync(60.00m, _annaMemberId, _borisMemberId);

        IReadOnlyList<GroupDto>? before =
            await _anna.GetAsync<IReadOnlyList<GroupDto>>(new Uri("/api/groups", UriKind.Relative));
        before!.Single().MyBalance.Should().Be(30.00m);

        await _anna.DeleteAsync(Expense(expense.Id));

        IReadOnlyList<GroupDto>? after =
            await _anna.GetAsync<IReadOnlyList<GroupDto>>(new Uri("/api/groups", UriKind.Relative));

        after!.Single().MyBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Delete_TakesTheExpenseOutOfTheFeed()
    {
        ExpenseDto kept = await CreateAsync(10.00m, _annaMemberId, _borisMemberId);
        ExpenseDto removed = await CreateAsync(20.00m, _annaMemberId, _borisMemberId);

        await _anna.DeleteAsync(Expense(removed.Id));

        ExpensePage page = (await _anna.GetAsync<ExpensePage>(Expenses))!;

        page.Items.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task Put_OnSomebodyElsesGroup_Answers404()
    {
        ExpenseDto expense = await CreateAsync(30.00m, _annaMemberId, _borisMemberId);

        (HttpClient boris, _) = await SignInAsync("boris@example.com");

        HttpResponseMessage response = await boris.PutAsync(
            Expense(expense.Id), EvenSplit(30.00m, _annaMemberId, _borisMemberId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_InAnArchivedGroup_Answers409()
    {
        ExpenseDto expense = await CreateAsync(30.00m, _annaMemberId, _borisMemberId);

        await _anna.PatchAsync(
            new Uri($"/api/groups/{_group.Id}", UriKind.Relative), new UpdateGroupRequest(null, true));

        HttpResponseMessage response = await _anna.PutAsync(
            Expense(expense.Id), EvenSplit(40.00m, _annaMemberId, _borisMemberId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(response)).Should().Be("group_archived");
    }
}
