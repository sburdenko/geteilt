using Geteilt.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace Geteilt.Api.Idempotency;

/// <summary>
/// Deletes idempotency records once they are older than their TTL.
/// </summary>
/// <remarks>
/// A <see cref="BackgroundService"/> rather than Hangfire: one periodic sweep does not justify a
/// scheduler, a dashboard and its own tables. It runs inside the API process, so a
/// second instance would simply sweep the same rows — the delete is idempotent in its own right.
/// </remarks>
public sealed partial class IdempotencyCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<IdempotencyCleanupService> logger) : BackgroundService
{
    internal static readonly TimeSpan RecordLifetime = TimeSpan.FromHours(24);

    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed {Count} expired idempotency records")]
    private static partial void LogSwept(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Idempotency cleanup failed; will try again next sweep")]
    private static partial void LogFailed(ILogger logger, Exception exception);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(SweepInterval, timeProvider);

        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A failed sweep must not take the host down with it: the records are a cache, and
                // the next sweep will pick up whatever this one missed.
                LogFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        IAppDbContext dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        DateTimeOffset cutoff = timeProvider.GetUtcNow() - RecordLifetime;

        int removed = await dbContext.IdempotencyRecords
            .Where(record => record.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
        {
            LogSwept(logger, removed);
        }

        return removed;
    }
}
