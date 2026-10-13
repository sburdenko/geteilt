using System.Net;
using System.Net.Http.Headers;
using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Contracts.Groups;
using Geteilt.Contracts.Members;
using Geteilt.Contracts.Settlements;
using Geteilt.Contracts.Users;
using Geteilt.Infrastructure.Encryption;
using Geteilt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Geteilt.Api.Tests;

/// <summary>What a database dump shows of an IBAN, and who can see one.</summary>
[Collection(SharedApiFixture.Name)]
public sealed class IbanStorageTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private const string AnnaIban = "DE89370400440532013000";
    private const string BorisIban = "DE02120300000000202051";

    private HttpClient _anna = null!;
    private Guid _annaId;
    private GroupDto _group = null!;

    public async Task InitializeAsync()
    {
        await factory.ResetAsync();

        (_anna, _annaId) = await SignInAsync("anna@example.com");

        HttpResponseMessage created = await _anna.PostAsync(
            new Uri("/api/groups", UriKind.Relative), new CreateGroupRequest("Hütte", "EUR"));
        _group = (await created.Content.ReadAsync<GroupDto>())!;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnAccountIban_IsEncryptedInTheDatabase_AndPlainThroughTheApi()
    {
        await _anna.PatchAsync(new Uri("/api/me", UriKind.Relative), new UpdateMeRequest(null, AnnaIban, null));

        string? stored = await RawAsync("SELECT iban AS \"Value\" FROM users WHERE id = {0}", _annaId);

        stored.Should().StartWith(IbanCipher.Prefix).And.NotContain(AnnaIban);
        UserDto me = (await (await _anna.GetAsync(new Uri("/api/me", UriKind.Relative))).Content.ReadAsync<UserDto>())!;
        me.Iban.Should().Be(AnnaIban);
    }

    [Fact]
    public async Task AMemberIban_IsEncryptedInTheDatabase_AndPlainThroughTheApi()
    {
        MemberDto boris = await AddMemberAsync("Boris", BorisIban);

        string? stored = await RawAsync("SELECT iban AS \"Value\" FROM members WHERE id = {0}", boris.Id);

        stored.Should().StartWith(IbanCipher.Prefix).And.NotContain(BorisIban);
        boris.Iban.Should().Be(BorisIban);
    }

    [Fact]
    public async Task TheDatabase_RefusesAnIbanWrittenInPlain()
    {
        // What an old version still running during a deploy would do.
        MemberDto boris = await AddMemberAsync("Boris", BorisIban);

        Func<Task> write = () => ExecuteAsync("UPDATE members SET iban = {0} WHERE id = {1}", BorisIban, boris.Id);

        await write.Should().ThrowAsync<Npgsql.PostgresException>().Where(error => error.SqlState == "23514");
    }

    [Fact]
    public async Task TheBackfill_EncryptsWhatWasStoredInPlain_AndIsSafeToRunTwice()
    {
        MemberDto boris = await AddMemberAsync("Boris", BorisIban);
        await StoreInPlainAsBeforeEncryptionAsync(boris.Id);

        int first = await Use(provider => provider.GetRequiredService<IbanBackfill>().RunAsync(CancellationToken.None));
        int second = await Use(provider => provider.GetRequiredService<IbanBackfill>().RunAsync(CancellationToken.None));

        first.Should().Be(1);
        second.Should().Be(0);
        (await RawAsync("SELECT iban AS \"Value\" FROM members WHERE id = {0}", boris.Id))
            .Should().StartWith(IbanCipher.Prefix);
        MemberDto[] members = (await (await _anna.GetAsync(MembersUri())).Content.ReadAsync<MemberDto[]>())!;
        members.Single(member => member.Id == boris.Id).Iban.Should().Be(BorisIban);
        (await ConstraintIsValidatedAsync()).Should().BeTrue("after the backfill every row is known to comply");
    }

    [Fact]
    public async Task WhileAnIbanIsStoredInPlain_TheApiIsNotReady()
    {
        // A skipped backfill stops the deploy at the readiness probe, instead of surfacing later
        // as failures on every screen of an affected group.
        MemberDto boris = await AddMemberAsync("Boris", BorisIban);
        await StoreInPlainAsBeforeEncryptionAsync(boris.Id);
        Uri ready = new("/api/health/ready", UriKind.Relative);

        HttpResponseMessage before = await _anna.GetAsync(ready);
        await Use(provider => provider.GetRequiredService<IbanBackfill>().RunAsync(CancellationToken.None));
        HttpResponseMessage after = await _anna.GetAsync(ready);

        before.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SomeoneOutsideTheGroup_ReachesNoIbanThroughAnyEndpoint()
    {
        MemberDto boris = await AddMemberAsync("Boris", BorisIban);
        (HttpClient outsider, _) = await SignInAsync("mallory@example.com");

        HttpResponseMessage[] answers =
        [
            await outsider.GetAsync(MembersUri()),
            await outsider.GetAsync(new Uri($"/api/groups/{_group.Id}/settlement-plan", UriKind.Relative)),
            await outsider.PostAsync(
                new Uri($"/api/groups/{_group.Id}/girocode/preview?format=json", UriKind.Relative),
                new GirocodePreviewRequest(boris.Id, 1.00m)),
        ];

        answers.Should().OnlyContain(answer => answer.StatusCode == HttpStatusCode.NotFound);

        foreach (HttpResponseMessage answer in answers)
        {
            (await answer.Content.ReadAsStringAsync()).Should().NotContain(BorisIban);
        }
    }

    /// <summary>
    /// The state a database is in right after the migration on a real deploy: rows from before
    /// encryption, under a constraint that only checks new writes until the backfill validates it.
    /// </summary>
    private async Task StoreInPlainAsBeforeEncryptionAsync(Guid memberId)
    {
        await ExecuteAsync("ALTER TABLE members DROP CONSTRAINT ck_members_iban_protected");
        await ExecuteAsync("UPDATE members SET iban = {0} WHERE id = {1}", BorisIban, memberId);
        await ExecuteAsync(
            "ALTER TABLE members ADD CONSTRAINT ck_members_iban_protected CHECK (iban IS NULL OR iban LIKE 'v1:%') NOT VALID");
    }

    private Task<bool> ConstraintIsValidatedAsync() =>
        Use(provider => provider.GetRequiredService<AppDbContext>().Database
            .SqlQueryRaw<bool>("SELECT convalidated AS \"Value\" FROM pg_constraint WHERE conname = 'ck_members_iban_protected'")
            .SingleAsync());

    private Uri MembersUri() => new($"/api/groups/{_group.Id}/members", UriKind.Relative);

    private async Task<MemberDto> AddMemberAsync(string name, string? iban)
    {
        HttpResponseMessage response = await _anna.PostAsync(MembersUri(), new AddMemberRequest(name, null, iban));

        return (await response.Content.ReadAsync<MemberDto>())!;
    }

    private Task<string?> RawAsync(string sql, Guid id) =>
        Use(provider => provider.GetRequiredService<AppDbContext>().Database
            .SqlQueryRaw<string?>(sql, id).SingleAsync());

    private Task<int> ExecuteAsync(string sql, params object[] arguments) =>
        Use(provider => provider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sql, arguments));

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
}
