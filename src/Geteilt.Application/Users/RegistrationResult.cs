namespace Geteilt.Application.Users;

/// <summary>Result of registering a new person.</summary>
public sealed record RegistrationResult
{
    private RegistrationResult(RegistrationOutcome outcome, Guid userId, IReadOnlyList<string> errors)
    {
        Outcome = outcome;
        UserId = userId;
        Errors = errors;
    }

    public RegistrationOutcome Outcome { get; }

    /// <summary>Empty unless <see cref="Outcome"/> is <see cref="RegistrationOutcome.Success"/>.</summary>
    public Guid UserId { get; }

    /// <summary>Reasons a password was turned down, safe to show to the person who typed it.</summary>
    public IReadOnlyList<string> Errors { get; }

    public bool Succeeded => Outcome == RegistrationOutcome.Success;

    public static RegistrationResult Success(Guid userId) =>
        new(RegistrationOutcome.Success, userId, []);

    public static RegistrationResult EmailAlreadyRegistered() =>
        new(RegistrationOutcome.EmailAlreadyRegistered, Guid.Empty, []);

    public static RegistrationResult PasswordRejected(IReadOnlyList<string> errors) =>
        new(RegistrationOutcome.PasswordRejected, Guid.Empty, errors);
}
