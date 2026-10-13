namespace Geteilt.Application.Users;

public enum CredentialOutcome
{
    Success,

    /// <summary>Wrong password, or no such account. The caller must not learn which.</summary>
    InvalidCredentials,

    LockedOut,
}

/// <summary><see cref="User"/> is only populated when the check succeeded.</summary>
public sealed record CredentialCheck(CredentialOutcome Outcome, AuthenticatedUser? User)
{
    public static CredentialCheck Success(AuthenticatedUser user) => new(CredentialOutcome.Success, user);

    public static CredentialCheck Failed(CredentialOutcome outcome) => new(outcome, null);
}
