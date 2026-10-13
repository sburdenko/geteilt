namespace Geteilt.Contracts.Members;

/// <summary>PATCH semantics: null leaves a field alone; an empty IBAN clears it.</summary>
public sealed record UpdateMemberRequest(string? DisplayName, string? Iban, MemberRole? Role);
