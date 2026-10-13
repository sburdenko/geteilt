using Geteilt.Application.Ports;
using Geteilt.Contracts.Users;
using Geteilt.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Geteilt.Application.Users;

/// <summary>Reading and editing the signed-in person's own profile.</summary>
public sealed class UserProfileService(IAppDbContext dbContext) : IUserProfileService
{
    public Task<UserDto?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserDto(
                user.Id, user.Email, user.DisplayName, user.Iban, user.PreferredCurrency))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<UserDto?> UpdateAsync(
        Guid userId,
        UpdateMeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Tracked, not projected: this one is going to be written back.
        User? user = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        // null means "leave alone". An empty IBAN is a different instruction —
        // it clears the field — which is why this checks for null rather than for emptiness.
        if (request.DisplayName is not null)
        {
            user.Rename(request.DisplayName);
        }

        if (request.Iban is not null)
        {
            user.SetIban(request.Iban);
        }

        if (request.PreferredCurrency is not null)
        {
            user.SetPreferredCurrency(request.PreferredCurrency);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UserDto(user.Id, user.Email, user.DisplayName, user.Iban, user.PreferredCurrency);
    }
}
