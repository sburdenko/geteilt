using System.ComponentModel.DataAnnotations;

namespace Geteilt.Contracts.Auth;

public sealed record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    [Required, StringLength(100, MinimumLength = 1)] string DisplayName);
