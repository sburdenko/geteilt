using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Geteilt.Api.Authentication;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The user id out of the token. Missing or unparseable means the authentication pipeline let
    /// through something it should have rejected — a bug, not a bad request, so it throws rather
    /// than returning null for every caller to re-check.
    /// </summary>
    internal static Guid CurrentUserId(this ClaimsPrincipal principal)
    {
        // Two names for one claim: the JWT carries "sub", and ASP.NET Core's default mapping
        // rewrites it to ClaimTypes.NameIdentifier depending on configuration.
        string? subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(subject, out Guid userId)
            ? userId
            : throw new InvalidOperationException("An authenticated request carries no usable subject claim.");
    }
}
