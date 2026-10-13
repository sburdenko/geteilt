namespace Geteilt.Application.Users;

/// <summary>
/// How a registration ended. An explicit outcome rather than an exception: a taken email is an
/// ordinary thing for a user to do, not an exceptional one.
/// </summary>
public enum RegistrationOutcome
{
    Success,
    EmailAlreadyRegistered,
    PasswordRejected,
}
