using System.Net;
using System.Text;
using Geteilt.Web;
using Geteilt.Web.Api;
using Geteilt.Web.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Geteilt.Web.Tests;

/// <summary>
/// The lifetimes, exercised through the real container rather than read.
/// </summary>
/// <remarks>
/// This exists because of a bug that every unit test here passed straight through:
/// IHttpClientFactory builds its handler chains in a scope of its own, so scoped auth state gave
/// AuthTokenHandler a different AuthState than the pages held. Sign-in worked, and the very next
/// request went out anonymous and came back 401. Only a test that goes through the container can
/// see that.
/// </remarks>
public sealed class ServiceWiringTests
{
    [Theory]
    [InlineData("groups")]
    [InlineData("members")]
    [InlineData("expenses")]
    [InlineData("balances")]
    public async Task EverySectionOfTheClient_CarriesTheTokenThatSigningInProduced(string section)
    {
        // Per section, not once: a new one is registered by hand, and forgetting its handler gives
        // exactly the failure this class exists for — anonymous requests from a signed-in app.
        StubHandler stub = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json"),
        });
        ServiceProvider provider = Build(stub);

        await provider.GetRequiredService<AuthState>()
            .SetAsync(Sessions.At("one", TimeSpan.FromMinutes(10)));

        GeteiltApiClient client = provider.GetRequiredService<GeteiltApiClient>();
        Guid group = Guid.CreateVersion7();

        await (section switch
        {
            "groups" => client.Groups.ListAsync(),
            "members" => client.Members.ListAsync(group),
            "expenses" => client.Expenses.ListAsync(group),
            _ => client.Balances.GetAsync(group) as Task,
        });

        stub.Seen.Should().ContainSingle().Which.BearerToken.Should().Be("access-one");
    }

    [Fact]
    public void TheStateEveryPartOfTheAppShares_IsOneInstance()
    {
        ServiceProvider provider = Build(new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)));

        using IServiceScope scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();

        scope.ServiceProvider.GetRequiredService<AuthState>()
            .Should().BeSameAs(provider.GetRequiredService<AuthState>());
        scope.ServiceProvider.GetRequiredService<SessionRenewer>()
            .Should().BeSameAs(provider.GetRequiredService<SessionRenewer>());
    }

    private static ServiceProvider Build(StubHandler stub)
    {
        ServiceCollection services = new();
        services.AddGeteiltWeb(new Uri("https://api.test/"), new Uri("https://app.test/"));

        // The real one talks to localStorage through IJSRuntime, which no test has.
        services.AddSingleton<ITokenStorage>(new InMemoryTokenStorage());

        // The graph registers the system clock, against which a session dated to a fixed instant
        // is long expired.
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(Sessions.Now));
        services.AddHttpClient<GroupsApi>().ConfigurePrimaryHttpMessageHandler(() => stub);
        services.AddHttpClient<MembersApi>().ConfigurePrimaryHttpMessageHandler(() => stub);
        services.AddHttpClient<ExpensesApi>().ConfigurePrimaryHttpMessageHandler(() => stub);
        services.AddHttpClient<BalancesApi>().ConfigurePrimaryHttpMessageHandler(() => stub);

        return services.BuildServiceProvider();
    }
}
