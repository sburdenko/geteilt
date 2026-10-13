using Geteilt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Geteilt.Api.Tests;

/// <summary>
/// Boots the real API against a real PostgreSQL in a container.
/// </summary>
/// <remarks>
/// Not the in-memory provider, on purpose: it has no transactions, ignores CHECK constraints and
/// is not a relational database at all, so a green test on it says nothing about what Postgres
/// will do. One container serves the whole test collection; data is cleared between
/// tests rather than the database rebuilt.
/// </remarks>
public sealed class GeteiltApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>A key for tests only — the real one never leaves the environment it runs in.</summary>
    public const string TestSigningKey = "test-signing-key-that-is-long-enough-for-hs256";

    public const string TestIssuer = "https://geteilt.test";
    public const string TestAudience = "https://geteilt.test";

    /// <summary>32 bytes, base64. The EF model caches the cipher with the first context, so every test shares one key.</summary>
    public const string TestIbanKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("geteilt_test")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    // Implemented explicitly: WebApplicationFactory already has a public DisposeAsync returning
    // ValueTask, and xUnit's IAsyncLifetime wants one returning Task.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Wipes the data between tests so one test cannot depend on another having run.</summary>
    public async Task ResetAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE users, app_users, app_user_claims, app_user_logins, app_user_tokens, refresh_tokens,\n             groups, members, expenses, expense_shares, settlements, idempotency_records CASCADE;");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Key", TestSigningKey);
        builder.UseSetting("Jwt:Issuer", TestIssuer);
        builder.UseSetting("Jwt:Audience", TestAudience);
        builder.UseSetting("IbanProtection:Key", TestIbanKey);

        // Production, so the startup migration does not race the one InitializeAsync runs, and so
        // the tests exercise the same pipeline shape the server does.
        builder.UseEnvironment("Production");

        builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>());
    }
}
