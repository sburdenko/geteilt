using Geteilt.Application.Ports;
using Geteilt.Domain.Girocode;
using QRCoder;

namespace Geteilt.Infrastructure.QrCodes;

/// <summary>
/// Renders a Girocode with QRCoder. Error correction stays at M, as EPC069-12 requires: higher
/// levels enlarge the symbol for no benefit here, lower ones stop surviving a phone camera at an angle.
/// </summary>
public sealed class GirocodeRenderer : IGirocodeRenderer
{
    public const int MinimumPixelSize = 512;

    private const QRCodeGenerator.ECCLevel ErrorCorrection = QRCodeGenerator.ECCLevel.M;

    public byte[] RenderPng(GirocodeRequest request, int pixelSize = MinimumPixelSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pixelSize, MinimumPixelSize);

        using QRCodeData data = Encode(request);
        PngByteQRCode png = new(data);

        // QRCoder sizes the image in module (pixel-per-square) units, so the requested edge length
        // has to be converted — and rounded up, never down, or the result lands under the minimum.
        int modulesPerSide = data.ModuleMatrix.Count;
        int pixelsPerModule = (int)Math.Ceiling((double)pixelSize / modulesPerSide);

        return png.GetGraphic(pixelsPerModule);
    }

    public string RenderSvg(GirocodeRequest request)
    {
        using QRCodeData data = Encode(request);
        SvgQRCode svg = new(data);

        return svg.GetGraphic(pixelsPerModule: 10);
    }

    private static QRCodeData Encode(GirocodeRequest request)
    {
        string payload = EpcQrPayload.Build(request);

        using QRCodeGenerator generator = new();

        return generator.CreateQrCode(payload, ErrorCorrection);
    }
}
