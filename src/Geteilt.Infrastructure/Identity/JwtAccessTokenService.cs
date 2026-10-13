using System.Security.Claims;
using System.Text;
using Geteilt.Application.Ports;
using Geteilt.Application.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Geteilt.Infrastructure.Identity;

/// <summary>
/// Signs access tokens with HS256. Symmetric because one service issues and the
/// same service verifies; asymmetric keys start paying off only when someone must verify without
/// being able to sign.
/// </summary>
public sealed class JwtAccessTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    : IAccessTokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken Issue(Guid userId, string email, string displayName)
    {
        DateTimeOffset issuedAt = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        // A token is signed, not encrypted: the payload is plain base64 and anyone holding the
        // token can read it. Id, email and display name are fine; an IBAN would not be
        //. Group membership is deliberately absent — it changes, and a token
        // already handed out cannot be withdrawn.
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Name, displayName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            ]),
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
                SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
