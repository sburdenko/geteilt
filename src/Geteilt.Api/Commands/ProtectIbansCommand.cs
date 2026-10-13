using Geteilt.Infrastructure.Encryption;

namespace Geteilt.Api.Commands;

/// <summary>
/// <c>dotnet Geteilt.Api.dll protect-ibans</c>: encrypts IBANs stored before encryption existed.
/// A deploy step of its own, run after the migrations and before the new version serves requests,
/// for the same reason migrations are.
/// </summary>
internal static partial class ProtectIbansCommand
{
    internal const string Name = "protect-ibans";

    [LoggerMessage(Level = LogLevel.Information, Message = "Encrypted {Count} IBANs that were stored in plain text")]
    private static partial void LogProtected(ILogger logger, int count);

    internal static async Task RunAsync(IServiceProvider services, ILogger logger)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        int count = await scope.ServiceProvider.GetRequiredService<IbanBackfill>().RunAsync(CancellationToken.None);

        LogProtected(logger, count);
    }
}
