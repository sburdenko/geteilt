using Geteilt.Application.Users;

namespace Geteilt.Application.Ports;

/// <summary>Issues the short-lived token the API authenticates with.</summary>
public interface IAccessTokenService
{
    AccessToken Issue(Guid userId, string email, string displayName);
}
