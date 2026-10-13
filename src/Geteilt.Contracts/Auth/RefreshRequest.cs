using System.ComponentModel.DataAnnotations;

namespace Geteilt.Contracts.Auth;

/// <summary>Also the body of logout: both operations act on one refresh token.</summary>
public sealed record RefreshRequest([Required] string RefreshToken);
