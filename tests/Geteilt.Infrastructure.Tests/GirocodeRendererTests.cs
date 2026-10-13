using Geteilt.Domain.Girocode;
using Geteilt.Infrastructure.QrCodes;

namespace Geteilt.Infrastructure.Tests;

public sealed class GirocodeRendererTests
{
    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly GirocodeRenderer _renderer = new();

    private static GirocodeRequest Request() => new()
    {
        BeneficiaryName = "Franz Mustermänn",
        Iban = "DE71110220330123456789",
        Amount = 12.30m,
        Currency = "EUR",
        Remittance = "Abendessen Ristorante",
    };

    [Fact]
    public void RenderPng_ProducesAPngFile()
    {
        byte[] png = _renderer.RenderPng(Request());

        png.Should().StartWith(PngSignature);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    public void RenderPng_IsAtLeastTheRequestedEdgeLength(int pixelSize)
    {
        byte[] png = _renderer.RenderPng(Request(), pixelSize);

        (int width, int height) = ReadPngDimensions(png);

        width.Should().Be(height);
        width.Should().BeGreaterThanOrEqualTo(pixelSize);
    }

    [Fact]
    public void RenderPng_BelowTheMinimumSize_Throws()
    {
        Action act = () => _renderer.RenderPng(Request(), GirocodeRenderer.MinimumPixelSize - 1);

        act.Should().ThrowExactly<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RenderPng_ForTheSameRequest_IsByteIdentical()
    {
        _renderer.RenderPng(Request()).Should().Equal(_renderer.RenderPng(Request()));
    }

    [Fact]
    public void RenderSvg_ProducesAnSvgDocument()
    {
        string svg = _renderer.RenderSvg(Request());

        svg.Should().StartWith("<svg").And.EndWith("</svg>");
    }

    [Fact]
    public void Render_PropagatesPayloadRejection()
    {
        Action act = () => _renderer.RenderPng(Request() with { Currency = "USD" });

        act.Should().ThrowExactly<EpcQrException>().Which.Code.Should().Be(EpcQrException.OnlyEuro.Code);
    }

    /// <summary>Width and height live in the IHDR chunk, big-endian, at a fixed offset.</summary>
    private static (int Width, int Height) ReadPngDimensions(byte[] png)
    {
        ReadOnlySpan<byte> bytes = png;

        return (
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4)),
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4)));
    }
}
