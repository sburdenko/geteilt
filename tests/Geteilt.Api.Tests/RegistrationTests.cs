using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Domain.Users;
using Geteilt.Infrastructure.Identity;
using Geteilt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Geteilt.Api.Tests;

[Collection(SharedApiFixture.Name)]
public sealed class RegistrationTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private const string GoodPassword = "Passwort123";

    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> UseAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        return await work(scope.ServiceProvider);
    }

    private Task<RegistrationResult> RegisterAsync(string email, string password = GoodPassword) =>
        UseAsync(provider => provider.GetRequiredService<IIdentityService>()
            .RegisterAsync(email, "Anna", password, CancellationToken.None));

    [Fact]
    public async Task Register_CreatesBothTheLoginAndTheDomainUser()
    {
        RegistrationResult result = await RegisterAsync("anna@example.com");

        result.Succeeded.Should().BeTrue();

        (AppUser? account, User? user) = await UseAsync(async provider =>
        {
            AppDbContext db = provider.GetRequiredService<AppDbContext>();

            return (
                await db.Users.SingleOrDefaultAsync(u => u.Id == result.UserId),
                await db.DomainUsers.SingleOrDefaultAsync(u => u.Id == result.UserId));
        });

        account.Should().NotBeNull();
        user.Should().NotBeNull();
        // Same Id on both halves is the whole point of keeping the domain clean of Identity.
        user!.Id.Should().Be(account!.Id);
        user.Email.Should().Be("anna@example.com");
    }

    [Fact]
    public async Task Register_WhenTheEmailIsTaken_ReportsItAndCreatesNothing()
    {
        await RegisterAsync("anna@example.com");

        RegistrationResult second = await RegisterAsync("anna@example.com");

        second.Outcome.Should().Be(RegistrationOutcome.EmailAlreadyRegistered);
        await CountsShouldBe(accounts: 1, users: 1);
    }

    [Theory]
    [InlineData("Kurz1")]
    [InlineData("Passwort1")]
    public async Task Register_WhenThePasswordIsShorterThanTen_IsRejectedAndCreatesNothing(string password)
    {
        RegistrationResult result = await RegisterAsync("anna@example.com", password);

        result.Outcome.Should().Be(RegistrationOutcome.PasswordRejected);
        await CountsShouldBe(accounts: 0, users: 0);
    }

    [Fact]
    public async Task Register_WhenThePasswordHasNoLetter_IsRejected()
    {
        RegistrationResult result = await RegisterAsync("anna@example.com", "1234567890");

        result.Outcome.Should().Be(RegistrationOutcome.PasswordRejected);
        result.Errors.Should().Contain(error => error.Contains("letter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_WhenThePasswordHasNoDigit_IsRejected()
    {
        RegistrationResult result = await RegisterAsync("anna@example.com", "Passwortlang");

        result.Outcome.Should().Be(RegistrationOutcome.PasswordRejected);
    }

    [Fact]
    public async Task Register_AcceptsMixedCaseEmailAndStoresItLowerCase()
    {
        RegistrationResult result = await RegisterAsync("Anna@Example.COM");

        result.Succeeded.Should().BeTrue();

        string? email = await UseAsync(async provider =>
            (await provider.GetRequiredService<AppDbContext>()
                .DomainUsers.SingleAsync(u => u.Id == result.UserId)).Email);

        email.Should().Be("anna@example.com");
    }

    [Fact]
    public async Task FiveWrongPasswords_LockTheAccount()
    {
        RegistrationResult registered = await RegisterAsync("anna@example.com");

        bool lockedOut = await UseAsync(async provider =>
        {
            UserManager<AppUser> users = provider.GetRequiredService<UserManager<AppUser>>();
            AppUser account = await users.FindByIdAsync(registered.UserId.ToString()) ?? throw new InvalidOperationException();

            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (!await users.CheckPasswordAsync(account, "falsches Passwort"))
                {
                    await users.AccessFailedAsync(account);
                }
            }

            return await users.IsLockedOutAsync(account);
        });

        lockedOut.Should().BeTrue();
    }

    [Fact]
    public async Task FourWrongPasswords_DoNotLockTheAccount()
    {
        RegistrationResult registered = await RegisterAsync("anna@example.com");

        bool lockedOut = await UseAsync(async provider =>
        {
            UserManager<AppUser> users = provider.GetRequiredService<UserManager<AppUser>>();
            AppUser account = await users.FindByIdAsync(registered.UserId.ToString()) ?? throw new InvalidOperationException();

            for (int attempt = 0; attempt < 4; attempt++)
            {
                await users.AccessFailedAsync(account);
            }

            return await users.IsLockedOutAsync(account);
        });

        lockedOut.Should().BeFalse();
    }

    private async Task CountsShouldBe(int accounts, int users)
    {
        (int actualAccounts, int actualUsers) = await UseAsync(async provider =>
        {
            AppDbContext db = provider.GetRequiredService<AppDbContext>();

            return (await db.Users.CountAsync(), await db.DomainUsers.CountAsync());
        });

        actualAccounts.Should().Be(accounts);
        actualUsers.Should().Be(users);
    }
}
