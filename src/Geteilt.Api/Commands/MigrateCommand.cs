using Geteilt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geteilt.Api.Commands;

/// <summary>
/// <c>dotnet Geteilt.Api.dll migrate</c>: applies the migrations and exits. On a server this is a
/// deploy step of its own, before the new version starts: a migration applied at startup takes
/// the application down with it when it fails, with nothing left running.
/// </summary>
internal static partial class MigrateCommand
{
    internal const string Name = "migrate";

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied {Count} migrations")]
    private static partial void LogApplied(ILogger logger, int count);

    internal static async Task RunAsync(IServiceProvider services, ILogger logger)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        int pending = (await dbContext.Database.GetPendingMigrationsAsync()).Count();
        await dbContext.Database.MigrateAsync();

        LogApplied(logger, pending);
    }
}
