using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Geteilt.Infrastructure.Encryption;

/// <summary>
/// Encrypts IBANs on their way into the database and back. AES-256-GCM:
/// a fresh nonce per value, and a tag that makes a tampered value fail instead of decrypting into
/// another account.
/// </summary>
/// <remarks>
/// Stored as <c>v1:</c> + base64(nonce | tag | ciphertext). The version is there for the day the
/// key or the scheme changes: old and new values can then be told apart and re-encrypted.
/// </remarks>
public sealed class IbanCipher
{
    public const string Prefix = "v1:";

    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int MaxIbanBytes = 34;

    /// <summary>The column length an encrypted IBAN of the longest allowed size needs.</summary>
    public const int MaxStoredLength = 128;

    // Read on first use rather than on construction: building the EF model needs this type, and
    // tools that only build the model (migrations) have no key and should not need one.
    private readonly Lazy<byte[]> _key;

    public IbanCipher(IOptions<IbanProtectionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _key = new Lazy<byte[]>(() =>
            options.Value.TryReadKey(out byte[] key)
                ? key
                : throw new InvalidOperationException(
                    $"{IbanProtectionOptions.SectionName}:Key is not a 32-byte base64 key."));
    }

    /// <summary>Fails now if there is no usable key, rather than at the first IBAN to encrypt.</summary>
    public void EnsureKey() => _ = _key.Value;

    public static bool IsProtected(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored.StartsWith(Prefix, StringComparison.Ordinal);
    }

    public string Protect(string iban)
    {
        ArgumentNullException.ThrowIfNull(iban);

        byte[] plaintext = Encoding.UTF8.GetBytes(iban);

        if (plaintext.Length > MaxIbanBytes)
        {
            // Unprotect refuses anything longer, so writing it would store a value nobody can read.
            throw new ArgumentException($"An IBAN has at most {MaxIbanBytes} characters.", nameof(iban));
        }
        byte[] sealedValue = new byte[NonceBytes + TagBytes + plaintext.Length];
        Span<byte> nonce = sealedValue.AsSpan(0, NonceBytes);
        Span<byte> tag = sealedValue.AsSpan(NonceBytes, TagBytes);
        Span<byte> ciphertext = sealedValue.AsSpan(NonceBytes + TagBytes);

        RandomNumberGenerator.Fill(nonce);

        using AesGcm aes = new(_key.Value, TagBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return Prefix + Convert.ToBase64String(sealedValue);
    }

    /// <exception cref="InvalidOperationException">The value was never encrypted: the backfill has not run.</exception>
    /// <exception cref="CryptographicException">The value was changed, garbled, or encrypted with another key.</exception>
    public string Unprotect(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        if (!IsProtected(stored))
        {
            throw new InvalidOperationException(
                "An IBAN in the database is not protected. Run the IBAN backfill before serving requests.");
        }

        byte[] sealedValue = new byte[NonceBytes + TagBytes + MaxIbanBytes];

        if (!Convert.TryFromBase64String(stored[Prefix.Length..], sealedValue, out int length)
            || length < NonceBytes + TagBytes)
        {
            throw new CryptographicException("A protected IBAN is garbled or has an impossible length.");
        }

        sealedValue = sealedValue[..length];

        ReadOnlySpan<byte> nonce = sealedValue.AsSpan(0, NonceBytes);
        ReadOnlySpan<byte> tag = sealedValue.AsSpan(NonceBytes, TagBytes);
        ReadOnlySpan<byte> ciphertext = sealedValue.AsSpan(NonceBytes + TagBytes);
        byte[] plaintext = new byte[ciphertext.Length];

        using AesGcm aes = new(_key.Value, TagBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
