namespace Geteilt.Contracts.Users;

/// <summary>
/// PATCH semantics: <see langword="null"/> means "leave this alone", not
/// "clear it". Clearing an IBAN is done by sending an empty string.
/// </summary>
public sealed record UpdateMeRequest(string? DisplayName, string? Iban, string? PreferredCurrency);
