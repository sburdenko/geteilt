namespace Geteilt.Web.Auth;

/// <summary>
/// Where the session survives a page reload. Behind an interface so the refresh logic can be
/// tested without a browser.
/// </summary>
public interface ITokenStorage
{
    ValueTask<AuthSession?> ReadAsync();

    ValueTask WriteAsync(AuthSession session);

    ValueTask ClearAsync();
}
