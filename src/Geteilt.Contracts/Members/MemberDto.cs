namespace Geteilt.Contracts.Members;

/// <param name="UserId">Null for someone taking part without an account of their own.</param>
/// <param name="RemovedAt">
/// Set once they have left. They stay in the response because they stay in the history: their old
/// expenses still count, and hiding them would break "all balances add up to zero".
/// </param>
public sealed record MemberDto(
    Guid Id,
    Guid? UserId,
    string DisplayName,
    string? Iban,
    MemberRole Role,
    bool HasAccount,
    DateTimeOffset? RemovedAt);
