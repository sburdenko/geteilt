using Geteilt.Domain.Girocode;
using Microsoft.AspNetCore.Mvc;

namespace Geteilt.Api.Controllers;

/// <summary>
/// Lets the error pipeline be exercised on a running instance. Every action answers 404 outside
/// Development, so the endpoints exist only where they are useful.
/// </summary>
[ApiController]
[Route("api/diagnostics")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class DiagnosticsController(IHostEnvironment environment) : ControllerBase
{
    /// <summary>Exercises the unhandled path: expect 500 with a traceId and no stack trace.</summary>
    [HttpGet("throw")]
    public IActionResult Throw()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        throw new InvalidOperationException("Deliberate failure from the diagnostics endpoint.");
    }

    /// <summary>Exercises the domain path: expect 422 with the machine-readable code.</summary>
    [HttpGet("throw-domain")]
    public IActionResult ThrowDomain()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        return Ok(EpcQrPayload.Build(new GirocodeRequest
        {
            BeneficiaryName = "Diagnostics",
            Iban = "DE71110220330123456789",
            Amount = 1.00m,
            Currency = "USD",
        }));
    }
}
