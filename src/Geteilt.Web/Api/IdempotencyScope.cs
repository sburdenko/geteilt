using System.Security.Cryptography;

namespace Geteilt.Web.Api;

/// <summary>
/// One screen's worth of POSTs that may be repeated after a lost answer. Within a scope, the same
/// body always gets the same <c>Idempotency-Key</c> and a different body a different one — the
/// same notion of "the same request" the server uses, which hashes the raw bytes.
/// </summary>
/// <remarks>
/// A type of its own rather than a <see cref="Guid"/>, so a scope cannot be passed where a ready
/// key is expected, and the other way round.
/// </remarks>
public readonly record struct IdempotencyScope(Guid Value)
{
    private const int VersionByte = 6;
    private const int VariantByte = 8;
    private const int GuidLength = 16;

    public static IdempotencyScope New() => new(Guid.CreateVersion7());

    /// <summary>
    /// A version 8 UUID (RFC 9562 §5.8) from SHA-256 over the scope and the body. Not a secret:
    /// it only has to be the same for the same bytes and differ for different ones.
    /// </summary>
    public Guid KeyFor(ReadOnlySpan<byte> body)
    {
        Span<byte> input = new byte[GuidLength + body.Length];
        Value.TryWriteBytes(input, bigEndian: true, out _);
        body.CopyTo(input[GuidLength..]);

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(input, digest);

        Span<byte> key = digest[..GuidLength];
        key[VersionByte] = (byte)((key[VersionByte] & 0x0F) | 0x80);
        key[VariantByte] = (byte)((key[VariantByte] & 0x3F) | 0x80);

        return new Guid(key, bigEndian: true);
    }
}
