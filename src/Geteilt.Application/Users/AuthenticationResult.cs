using Geteilt.Contracts.Auth;

namespace Geteilt.Application.Users;

public enum AuthenticationOutcome
{
    Success,
    EmailAlreadyRegistered,
    PasswordRejected,
    InvalidCredentials,
    LockedOut,

    /// <summary>The refresh token was unknown, expired, or already spent.</summary>
    RefreshRejected,
}

public sealed record AuthenticationResult
{
    private AuthenticationResult(AuthenticationOutcome outcome, AuthResponse? response, IReadOnlyList<string> errors)
    {
        Outcome = outcome;
        Response = response;
        Errors = errors;
    }

    public AuthenticationOutcome Outcome { get; }

    public AuthResponse? Response { get; }

    /// <summary>Only ever password-policy messages, which are safe to show to whoever typed them.</summary>
    public IReadOnlyList<string> Errors { get; }

    public bool Succeeded => Outcome == AuthenticationOutcome.Success;

    public static AuthenticationResult Success(AuthResponse response) =>
        new(AuthenticationOutcome.Success, response, []);

    public static AuthenticationResult Failed(AuthenticationOutcome outcome, IReadOnlyList<string>? errors = null) =>
        new(outcome, null, errors ?? []);
}
