namespace Geteilt.Api.Commands;

/// <summary>
/// The steps of a deploy that run once and exit instead of serving: <c>migrate</c>, then
/// <c>protect-ibans</c>, then the new version starts.
/// </summary>
internal static class DeployCommands
{
    /// <returns>Whether a command ran, in which case the process should exit.</returns>
    internal static async Task<bool> TryRunAsync(IServiceProvider services, ILogger logger, string[] args)
    {
        switch (args.FirstOrDefault())
        {
            case MigrateCommand.Name:
                await MigrateCommand.RunAsync(services, logger);
                return true;

            case ProtectIbansCommand.Name:
                await ProtectIbansCommand.RunAsync(services, logger);
                return true;

            default:
                return false;
        }
    }
}
