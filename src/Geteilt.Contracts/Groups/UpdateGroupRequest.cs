namespace Geteilt.Contracts.Groups;

/// <summary>
/// PATCH semantics: null leaves a field alone. There is deliberately no
/// BaseCurrency here — changing it would mean reconverting every historical expense at historical
/// rates, an operation with no single right answer, so a different currency means a new group
///.
/// </summary>
public sealed record UpdateGroupRequest(string? Name, bool? IsArchived);
