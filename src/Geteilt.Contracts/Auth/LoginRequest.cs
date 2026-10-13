using System.ComponentModel.DataAnnotations;

namespace Geteilt.Contracts.Auth;

public sealed record LoginRequest(
    [Required] string Email,
    [Required] string Password);
