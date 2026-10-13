using System.ComponentModel.DataAnnotations;

namespace Geteilt.Infrastructure.Identity;

/// <summary>Token settings. Validated on start, so bad configuration is a startup failure.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HS256 signing key. At least 32 bytes, because a shorter key is shorter than the SHA-256
    /// output it feeds and weakens the signature. Never in git — user-secrets locally,
    /// environment variables on the server.
    /// </summary>
    [Required]
    [MinLength(MinimumKeyLength)]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Short on purpose: an issued token cannot be withdrawn, so the window in which a stolen one
    /// is useful has to be small. Continuity comes from the refresh token instead.
    /// </summary>
    [Range(1, 60)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 30;

    public const int MinimumKeyLength = 32;
}
