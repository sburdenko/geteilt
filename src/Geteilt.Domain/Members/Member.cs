namespace Geteilt.Domain.Members;

/// <summary>
/// Someone taking part in a group. Not the same thing as a <see cref="Users.User"/>: a member may have
/// no account at all, which is how you add a friend to a dinner without making them sign up.
/// </summary>
public sealed class Member
{
    public const int MaxDisplayNameLength = 100;

    public Member(
        Guid id,
        Guid groupId,
        string displayName,
        DateTimeOffset joinedAt,
        Guid? userId = null,
        MemberRole role = MemberRole.Member,
        string? iban = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(displayName.Length, MaxDisplayNameLength);

        Id = id;
        GroupId = groupId;
        DisplayName = displayName;
        JoinedAt = joinedAt;
        UserId = userId;
        Role = role;
        SetIban(iban);
    }

    /// <summary>Materialisation constructor for EF Core.</summary>
    private Member()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    /// <summary>Null for a participant without an account.</summary>
    public Guid? UserId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Falls back to <see cref="User.Iban"/> when null; if both are null, no Girocode.</summary>
    public string? Iban { get; private set; }

    public MemberRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>
    /// Marks someone as no longer picked for new expenses. They stay in every past expense and in
    /// the balance calculation — removing them outright would break "all balances sum to zero"
    /// retroactively.
    /// </summary>
    public DateTimeOffset? RemovedAt { get; private set; }

    public bool IsActive => RemovedAt is null;

    public void Remove(DateTimeOffset removedAt) => RemovedAt ??= removedAt;

    public void SetIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            Iban = null;
            return;
        }

        // Same reasoning as User.SetIban: this arrives from a form, so it owes the caller
        // 422 invalid_iban rather than a 500.
        if (!Girocode.Iban.TryNormalise(iban, out string normalised))
        {
            throw DomainValidationException.ForIban(iban);
        }

        Iban = normalised;
    }

    public void Rename(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(displayName.Length, MaxDisplayNameLength);

        DisplayName = displayName;
    }

    public void ChangeRole(MemberRole role) => Role = role;
}
