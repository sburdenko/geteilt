using Geteilt.Api.Authentication;
using Geteilt.Api.Idempotency;
using Geteilt.Api.Logging;
using Geteilt.Application.Ports;
using Geteilt.Application.Settlements;
using Geteilt.Contracts.Settlements;
using Geteilt.Domain.Girocode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Geteilt.Api.Controllers;

/// <summary>
/// The Girocode: the whole point of the product. A German banking app scans it and fills in the
/// transfer, so nobody types an IBAN by hand.
/// </summary>
[ApiController]
[Authorize]
public sealed partial class GirocodeController(
    IGirocodeService girocodes,
    IGirocodeRenderer renderer,
    ILogger<GirocodeController> logger) : ControllerBase
{
    private const int PixelSize = 640;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Girocode issued for {Amount} {Currency} to an account ending {IbanTail}")]
    private static partial void LogIssued(ILogger logger, decimal amount, string currency, string ibanTail);

    [HttpGet("api/settlements/{settlementId:guid}/girocode")]
    [Produces("image/png", "image/svg+xml", "text/plain", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ForSettlement(
        Guid settlementId,
        CancellationToken cancellationToken,
        [FromQuery] string format = "png")
    {
        GirocodeRequest? request = await girocodes.ForSettlementAsync(
            User.CurrentUserId(), settlementId, cancellationToken);

        return request is null ? NotFound() : Render(request, format);
    }

    [HttpPost("api/groups/{groupId:guid}/girocode/preview")]
    [SkipIdempotency]
    [Produces("image/png", "image/svg+xml", "text/plain", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Preview(
        Guid groupId,
        GirocodePreviewRequest body,
        CancellationToken cancellationToken,
        [FromQuery] string format = "png")
    {
        ArgumentNullException.ThrowIfNull(body);

        // A POST that writes nothing: it takes a body only because a QR for an arbitrary amount
        // does not fit in a path. Nothing to repeat, so no Idempotency-Key.
        GirocodeRequest? request = await girocodes.ForPreviewAsync(
            User.CurrentUserId(), groupId, body.ToMemberId, body.Amount, cancellationToken);

        return request is null ? NotFound() : Render(request, format);
    }

    private IActionResult Render(GirocodeRequest request, string format)
    {
        // Only the last four characters of the IBAN reach the log. A full one there would sit in
        // whatever collects logs, readable by anyone with access to it.
        //
        // Guarded because the truncation is an argument, and arguments are evaluated whether or
        // not the level is enabled — cheap here, but the habit is what keeps it cheap.
        if (logger.IsEnabled(LogLevel.Information))
        {
            string ibanTail = IbanLog.Tail(request.Iban);

            LogIssued(logger, request.Amount, request.Currency, ibanTail);
        }

        return format.ToUpperInvariant() switch
        {
            "JSON" => Ok(new GirocodeDto(
                request.BeneficiaryName,
                request.Iban,
                request.Amount,
                request.Currency,
                request.Remittance,
                Convert.ToBase64String(renderer.RenderPng(request, PixelSize)))),
            "TEXT" => Content(EpcQrPayload.Build(request), "text/plain"),
            "SVG" => Content(renderer.RenderSvg(request), "image/svg+xml"),
            _ => File(renderer.RenderPng(request, PixelSize), "image/png"),
        };
    }
}
