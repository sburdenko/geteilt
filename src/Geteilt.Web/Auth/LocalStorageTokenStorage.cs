using System.Text.Json;
using Geteilt.Contracts;
using Microsoft.JSInterop;

namespace Geteilt.Web.Auth;

/// <summary>
/// Tokens in <c>localStorage</c>: exposed to XSS, where an httpOnly
/// cookie would be exposed to CSRF and would not serve the mobile client at all. The mitigation
/// is a strict CSP, not a different store.
/// </summary>
public sealed class LocalStorageTokenStorage(IJSRuntime js) : ITokenStorage
{
    private const string Key = "geteilt.session";

    public async ValueTask<AuthSession?> ReadAsync()
    {
        string? stored = await js.InvokeAsync<string?>("localStorage.getItem", Key);

        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AuthSession>(stored, GeteiltJson.Options);
        }
        catch (JsonException)
        {
            // A session written by an older build, or one somebody edited by hand. Treat it as
            // signed out rather than letting the app fail to start.
            await ClearAsync();

            return null;
        }
    }

    public async ValueTask WriteAsync(AuthSession session) =>
        await js.InvokeVoidAsync(
            "localStorage.setItem", Key, JsonSerializer.Serialize(session, GeteiltJson.Options));

    public async ValueTask ClearAsync() => await js.InvokeVoidAsync("localStorage.removeItem", Key);
}
