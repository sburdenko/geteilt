using Microsoft.JSInterop;

namespace Geteilt.Web.Localisation;

public sealed class BrowserLanguagePreference(IJSRuntime js) : ILanguagePreference
{
    private const string Key = "geteilt.language";

    public async ValueTask<string?> ReadAsync() =>
        await js.InvokeAsync<string?>("localStorage.getItem", Key);

    public async ValueTask WriteAsync(string language) =>
        await js.InvokeVoidAsync("localStorage.setItem", Key, language);

    public async ValueTask<IReadOnlyList<string>> BrowserPreferencesAsync() =>
        await js.InvokeAsync<string[]>("geteilt.browserLanguages");
}
