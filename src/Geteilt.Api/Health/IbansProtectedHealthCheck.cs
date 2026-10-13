using Geteilt.Infrastructure.Encryption;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Geteilt.Api.Health;

/// <summary>
/// Not ready while any IBAN is stored in plain text: the <c>protect-ibans</c> deploy step was
/// skipped, and every group holding such a value would fail to load. Failing here stops
/// the deploy at the probe instead.
/// </summary>
internal sealed class IbansProtectedHealthCheck(IbanBackfill backfill) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        int unprotected = await backfill.CountUnprotectedAsync(cancellationToken);

        return unprotected == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"{unprotected} IBANs are stored in plain text; run protect-ibans.");
    }
}
