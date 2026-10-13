using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Geteilt.Domain.Users;
using Geteilt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Geteilt.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<AppUser> userManager,
    AppDbContext dbContext,
    TimeProvider timeProvider) : IIdentityService
{
    public async Task<RegistrationResult> RegisterAsync(
        string email,
        string displayName,
        string password,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        string normalisedEmail = email.Trim().ToLowerInvariant();

        // Both writes go through the same DbContext, which is also the Identity store, so one
        // transaction covers them. Half a registration — an account that can log in but has no
        // domain user — would be unrecoverable without manual surgery.
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        AppUser appUser = new()
        {
            Id = Guid.CreateVersion7(),
            UserName = normalisedEmail,
            Email = normalisedEmail,
        };

        IdentityResult created = await userManager.CreateAsync(appUser, password);

        if (!created.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);

            return created.Errors.Any(IsDuplicate)
                ? RegistrationResult.EmailAlreadyRegistered()
                : RegistrationResult.PasswordRejected([.. created.Errors.Select(error => error.Description)]);
        }

        dbContext.DomainUsers.Add(new User(appUser.Id, normalisedEmail, displayName, timeProvider.GetUtcNow()));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RegistrationResult.Success(appUser.Id);
    }

    public async Task<CredentialCheck> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        AppUser? account = await userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());

        if (account is null)
        {
            // No such address. Answering the same as "wrong password" costs one wasted hash and
            // keeps the login form from doubling as a way to enumerate who has an account here.
            return CredentialCheck.Failed(CredentialOutcome.InvalidCredentials);
        }

        if (await userManager.IsLockedOutAsync(account))
        {
            return CredentialCheck.Failed(CredentialOutcome.LockedOut);
        }

        if (!await userManager.CheckPasswordAsync(account, password))
        {
            await userManager.AccessFailedAsync(account);

            return await userManager.IsLockedOutAsync(account)
                ? CredentialCheck.Failed(CredentialOutcome.LockedOut)
                : CredentialCheck.Failed(CredentialOutcome.InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(account);

        AuthenticatedUser? user = await FindAsync(account.Id, cancellationToken);

        return user is null
            ? CredentialCheck.Failed(CredentialOutcome.InvalidCredentials)
            : CredentialCheck.Success(user);
    }

    public Task<AuthenticatedUser?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.DomainUsers
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new AuthenticatedUser(
                user.Id, user.Email, user.DisplayName, user.Iban, user.PreferredCurrency))
            .SingleOrDefaultAsync(cancellationToken);

    private static bool IsDuplicate(IdentityError error) =>
        error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName);
}
