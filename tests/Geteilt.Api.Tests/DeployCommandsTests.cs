using Geteilt.Api.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Geteilt.Api.Tests;

/// <summary>The steps deploy.sh runs before the new version takes requests.</summary>
[Collection(SharedApiFixture.Name)]
public sealed class DeployCommandsTests(GeteiltApiFactory factory)
{
    private static readonly ILogger Logger = NullLogger.Instance;

    [Theory]
    [InlineData]
    [InlineData("--urls", "http://0.0.0.0:8080")]
    [InlineData("migrations")]
    public async Task AnythingButAKnownCommand_StartsTheServerAsUsual(params string[] args)
    {
        (await DeployCommands.TryRunAsync(factory.Services, Logger, args)).Should().BeFalse();
    }

    [Theory]
    [InlineData("migrate")]
    [InlineData("protect-ibans")]
    public async Task AKnownCommand_RunsAndTellsTheProcessToExit(string command)
    {
        // Against the test database, already migrated and with nothing to encrypt: both steps are
        // idempotent, which is what lets deploy.sh run them on every release.
        (await DeployCommands.TryRunAsync(factory.Services, Logger, [command])).Should().BeTrue();
    }

    [Fact]
    public async Task Migrate_LeavesNoMigrationPending()
    {
        await DeployCommands.TryRunAsync(factory.Services, Logger, ["migrate"]);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IEnumerable<string> pending = await scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.AppDbContext>().Database.GetPendingMigrationsAsync();

        pending.Should().BeEmpty();
    }
}
