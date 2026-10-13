using Geteilt.Web.Api;
using Geteilt.Web.Auth;
using Geteilt.Web.Localisation;
using Geteilt.Web.Notifications;
using Geteilt.Web.Settlements;
using Microsoft.Extensions.DependencyInjection;

namespace Geteilt.Web;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires the browser client. Kept out of <c>Program</c> so the graph can be built in a test:
    /// the way these lifetimes interact is not something reading the code reliably catches.
    /// </summary>
    public static IServiceCollection AddGeteiltWeb(
        this IServiceCollection services,
        Uri apiBaseAddress,
        Uri appBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);

        // Singletons, not scoped, and the difference is load-bearing. IHttpClientFactory builds
        // its handler chains inside a scope of its own, so a scoped AuthState would hand
        // AuthTokenHandler a different instance than the one the pages write to — the handler
        // would see nobody signed in and send every request anonymously. Beyond that, one browser
        // tab has exactly one signed-in person, and a singleton is what says so.
        services.AddSingleton<ITokenStorage, LocalStorageTokenStorage>();
        services.AddSingleton<AuthState>();
        services.AddSingleton<SessionRenewer>();
        services.AddSingleton<ToastService>();

        // Same lifetime and the same reason as the auth state above.
        services.AddSingleton<ILanguagePreference, BrowserLanguagePreference>();
        services.AddSingleton<LanguageState>();
        services.AddSingleton<Localizer>();
        services.AddHttpClient<ITranslationSource, HttpTranslationSource>(
            client => client.BaseAddress = appBaseAddress);

        services.AddHttpClient<AuthApi>(client => client.BaseAddress = apiBaseAddress);
        services.AddSingleton<ITokenRefresher>(provider => provider.GetRequiredService<AuthApi>());

        services.AddTransient<AuthTokenHandler>();
        // Every section goes through AuthTokenHandler, so no call can quietly skip the token or
        // the transparent renewal behind it.
        services.AddHttpClient<GroupsApi>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<AuthTokenHandler>();
        services.AddHttpClient<MembersApi>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<AuthTokenHandler>();
        services.AddHttpClient<ExpensesApi>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<AuthTokenHandler>();
        services.AddHttpClient<BalancesApi>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<AuthTokenHandler>();
        services.AddHttpClient<SettlementsApi>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<AuthTokenHandler>();
        services.AddHttpClient<GirocodeApi>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<AuthTokenHandler>();

        services.AddSingleton<GeteiltApiClient>();
        services.AddTransient<SettleFlow>();

        return services;
    }
}
