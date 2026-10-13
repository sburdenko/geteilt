namespace Geteilt.Contracts.Users;

/// <summary>The signed-in person as the API returns them.</summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? Iban,
    string PreferredCurrency);
