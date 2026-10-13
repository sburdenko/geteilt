namespace Geteilt.Infrastructure.Encryption;

/// <summary>
/// The key IBANs are encrypted with. Never in git and never in the image:
/// it arrives from the environment, like the JWT key. Validated on start, so a missing or short key
/// is a startup failure rather than the first encrypted write failing.
/// </summary>
public sealed class IbanProtectionOptions
{
    public const string SectionName = "IbanProtection";

    public const int KeyBytes = 32;

    /// <summary>AES-256 key, base64. Generate one with <c>openssl rand -base64 32</c>.</summary>
    public string Key { get; init; } = string.Empty;

    public bool HasValidKey() => TryReadKey(out _);

    internal bool TryReadKey(out byte[] key)
    {
        key = new byte[KeyBytes];

        return Convert.TryFromBase64String(Key, key, out int written) && written == KeyBytes;
    }
}
