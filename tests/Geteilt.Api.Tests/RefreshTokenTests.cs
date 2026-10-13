using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Infrastructure.Identity;
using Geteilt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Geteilt.Api.Tests;

/// <summary>Refresh tokens: rotation on every use, and what happens when a token is used twice.</summary>
[Collection(SharedApiFixture.Name)]
public sealed class RefreshTokenTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await factory.ResetAsync();

        _userId = (await UseAsync(provider => provider.GetRequiredService<IIdentityService>()
            .RegisterAsync("anna@example.com", "Anna", "Passwort123", CancellationToken.None))).UserId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> UseAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        return await work(scope.ServiceProvider);
    }

    private Task<IssuedRefreshToken> IssueAsync() =>
        UseAsync(provider => provider.GetRequiredService<IRefreshTokenService>()
            .IssueAsync(_userId, "203.0.113.7", CancellationToken.None));

    private Task<RefreshResult> RedeemAsync(string token) =>
        UseAsync(provider => provider.GetRequiredService<IRefreshTokenService>()
            .RedeemAsync(token, "203.0.113.7", CancellationToken.None));

    private Task<List<RefreshToken>> StoredTokensAsync() =>
        UseAsync(provider => provider.GetRequiredService<AppDbContext>()
            .RefreshTokens.AsNoTracking().OrderBy(token => token.CreatedAt).ToListAsync());

    [Fact]
    public async Task Issue_StoresAHashAndNeverTheTokenItself()
    {
        IssuedRefreshToken issued = await IssueAsync();

        List<RefreshToken> stored = await StoredTokensAsync();

        stored.Should().ContainSingle();
        stored[0].TokenHash.Should().NotBe(issued.Value);
        stored[0].TokenHash.Should().HaveLength(64).And.MatchRegex("^[0-9A-F]+$");

        // Whatever the database leaks, it must not leak working sessions.
        stored.Should().NotContain(token => token.TokenHash.Contains(issued.Value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Issue_GivesEachTokenADifferentValue()
    {
        IssuedRefreshToken first = await IssueAsync();
        IssuedRefreshToken second = await IssueAsync();

        second.Value.Should().NotBe(first.Value);
    }

    [Fact]
    public async Task Redeem_ReturnsANewTokenAndSpendsTheOldOne()
    {
        IssuedRefreshToken issued = await IssueAsync();

        RefreshResult result = await RedeemAsync(issued.Value);

        result.Succeeded.Should().BeTrue();
        result.UserId.Should().Be(_userId);
        result.Token.Should().NotBe(issued.Value);

        List<RefreshToken> stored = await StoredTokensAsync();
        stored.Should().HaveCount(2);
        stored[0].RevokedAt.Should().NotBeNull();
        stored[0].ReplacedByTokenId.Should().Be(stored[1].Id);
        stored[1].RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Redeem_WithAnUnknownToken_ReportsNotFound()
    {
        RefreshResult result = await RedeemAsync("this-token-was-never-issued");

        result.Outcome.Should().Be(RefreshOutcome.NotFound);
    }

    [Fact]
    public async Task Redeem_AfterExpiry_ReportsExpired()
    {
        IssuedRefreshToken issued = await IssueAsync();

        // Aged in the database rather than by moving the clock: the fixture is shared, and a frozen
        // clock would leak into every other test in the collection.
        await UseAsync(async provider =>
            await provider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
                "UPDATE refresh_tokens SET expires_at = now() - interval '1 day';"));

        RefreshResult result = await RedeemAsync(issued.Value);

        result.Outcome.Should().Be(RefreshOutcome.Expired);
    }

    [Fact]
    public async Task Redeem_AfterLogout_ReportsReuse()
    {
        IssuedRefreshToken issued = await IssueAsync();

        await UseAsync(async provider =>
        {
            await provider.GetRequiredService<IRefreshTokenService>()
                .RevokeAsync(issued.Value, CancellationToken.None);
            return true;
        });

        RefreshResult result = await RedeemAsync(issued.Value);

        result.Outcome.Should().Be(RefreshOutcome.Reused);
    }

    [Fact]
    public async Task StolenToken_UsedAfterTheOwner_KillsBothSessions()
    {
        // Anna signs in; the token is copied off her machine.
        IssuedRefreshToken stolen = await IssueAsync();

        // Anna refreshes first and moves on to the successor.
        RefreshResult annas = await RedeemAsync(stolen.Value);
        annas.Succeeded.Should().BeTrue();

        // The thief now presents the copy. It was already spent, which is the giveaway.
        RefreshResult thiefs = await RedeemAsync(stolen.Value);
        thiefs.Outcome.Should().Be(RefreshOutcome.Reused);

        // Anna's successor dies with it: which of the two is the thief cannot be known, so the
        // session ends for both and whoever is real signs in again.
        RefreshResult annasSuccessor = await RedeemAsync(annas.Token);
        annasSuccessor.Outcome.Should().Be(RefreshOutcome.Reused);

        List<RefreshToken> stored = await StoredTokensAsync();
        stored.Should().OnlyContain(token => token.RevokedAt != null);
    }

    [Fact]
    public async Task StolenToken_UsedBeforeTheOwner_KillsBothSessions()
    {
        IssuedRefreshToken stolen = await IssueAsync();

        // This time the thief gets there first.
        RefreshResult thiefs = await RedeemAsync(stolen.Value);
        thiefs.Succeeded.Should().BeTrue();

        RefreshResult annas = await RedeemAsync(stolen.Value);
        annas.Outcome.Should().Be(RefreshOutcome.Reused);

        RefreshResult thiefsSuccessor = await RedeemAsync(thiefs.Token);
        thiefsSuccessor.Outcome.Should().Be(RefreshOutcome.Reused);
    }

    [Fact]
    public async Task ReusingTheFirstToken_RevokesEveryDescendant()
    {
        IssuedRefreshToken first = await IssueAsync();

        RefreshResult second = await RedeemAsync(first.Value);
        RefreshResult third = await RedeemAsync(second.Token);
        RefreshResult fourth = await RedeemAsync(third.Token);

        // Going back to the very first token has to take the entire chain down, not just the link
        // immediately after it.
        (await RedeemAsync(first.Value)).Outcome.Should().Be(RefreshOutcome.Reused);

        (await RedeemAsync(fourth.Token)).Outcome.Should().Be(RefreshOutcome.Reused);

        List<RefreshToken> stored = await StoredTokensAsync();
        stored.Should().HaveCount(4);
        stored.Should().OnlyContain(token => token.RevokedAt != null);
    }

    [Fact]
    public async Task RevokingAChain_LeavesOtherSessionsAlone()
    {
        // A second device signs in separately: its chain is unrelated and must survive.
        IssuedRefreshToken laptop = await IssueAsync();
        IssuedRefreshToken phone = await IssueAsync();

        RefreshResult rotatedLaptop = await RedeemAsync(laptop.Value);
        (await RedeemAsync(laptop.Value)).Outcome.Should().Be(RefreshOutcome.Reused);
        rotatedLaptop.Should().NotBeNull();

        RefreshResult phoneStillWorks = await RedeemAsync(phone.Value);

        phoneStillWorks.Succeeded.Should().BeTrue();
    }
}
