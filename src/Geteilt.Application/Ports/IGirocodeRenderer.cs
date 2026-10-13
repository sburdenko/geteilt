using Geteilt.Domain.Girocode;

namespace Geteilt.Application.Ports;

/// <summary>
/// Turns a Girocode payload into something a camera can read. Declared here and implemented in
/// Infrastructure, because drawing needs a package and the layers above must not know which one.
/// </summary>
public interface IGirocodeRenderer
{
    /// <param name="request">What the code should contain.</param>
    /// <param name="pixelSize">Edge length of the square image, at least 512.</param>
    byte[] RenderPng(GirocodeRequest request, int pixelSize = 512);

    string RenderSvg(GirocodeRequest request);
}
