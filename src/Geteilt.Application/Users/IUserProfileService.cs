using Geteilt.Contracts.Users;

namespace Geteilt.Application.Users;

public interface IUserProfileService
{
    Task<UserDto?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns <see langword="null"/> when there is no such user.</summary>
    Task<UserDto?> UpdateAsync(Guid userId, UpdateMeRequest request, CancellationToken cancellationToken);
}
