namespace Geteilt.Contracts.Groups;

/// <param name="MyBalance">
/// What the caller is owed (positive) or owes (negative) in this group, in its base currency.
/// </param>
public sealed record GroupDto(
    Guid Id,
    string Name,
    string BaseCurrency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt,
    int MemberCount,
    decimal MyBalance);
