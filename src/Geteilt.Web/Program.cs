using Geteilt.Web;
using Geteilt.Web.Auth;
using Geteilt.Web.Localisation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same origin when the API serves wwwroot; wwwroot/appsettings.json overrides it for a split
// deployment, so the address is configuration rather than a rebuild.
// The translation files are served next to the app, which is not always where the API lives.
builder.Services.AddGeteiltWeb(
    ApiAddress.Resolve(builder.Configuration["ApiBaseAddress"], builder.HostEnvironment.BaseAddress),
    new Uri(builder.HostEnvironment.BaseAddress));

WebAssemblyHost host = builder.Build();

// Both before the first render: the session so a reload lands where the person was instead of at
// the login screen, the language so nothing paints in the wrong one first.
await host.Services.GetRequiredService<LanguageState>().LoadAsync();
await host.Services.GetRequiredService<AuthState>()
    .RestoreAsync(host.Services.GetRequiredService<TimeProvider>());

await host.RunAsync();
