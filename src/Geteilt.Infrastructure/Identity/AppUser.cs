using Microsoft.AspNetCore.Identity;

namespace Geteilt.Infrastructure.Identity;

/// <summary>
/// The authentication half of a person: password hash, lockout, security stamp.
/// </summary>
/// <remarks>
/// Lives here rather than in the domain because <see cref="IdentityUser{TKey}"/> drags
/// Microsoft.AspNetCore.Identity in with it, and the domain references nothing. The domain has its
/// own <see cref="Domain.Users.User"/> sharing this Id one-to-one. It reads as duplication and
/// buys exactly one thing: the authentication provider can be swapped without the domain noticing
///.
/// </remarks>
public sealed class AppUser : IdentityUser<Guid>
{
}
