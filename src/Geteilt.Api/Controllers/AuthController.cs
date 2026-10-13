using Geteilt.Api.Errors;
using Geteilt.Api.Idempotency;
using Geteilt.Api.RateLimiting;
using Geteilt.Application.Users;
using Geteilt.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Geteilt.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
// Signing in is naturally repeatable and registration is already guarded by the unique email, so
// neither carries an Idempotency-Key. They are also anonymous, and a key is scoped to a person.
[SkipIdempotency]
public sealed class AuthController(IAuthenticationService authentication) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        AuthenticationResult result = await authentication.RegisterAsync(
            request.Email, request.DisplayName, request.Password, ClientIp(), cancellationToken);

        return result.Succeeded
            ? Created((string?)null, result.Response)
            : Problem(result);
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        AuthenticationResult result = await authentication.LoginAsync(
            request.Email, request.Password, ClientIp(), cancellationToken);

        return result.Succeeded ? Ok(result.Response) : Problem(result);
    }

    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        RefreshRequest request,
        CancellationToken cancellationToken)
    {
        AuthenticationResult result = await authentication.RefreshAsync(
            request.RefreshToken, ClientIp(), cancellationToken);

        return result.Succeeded ? Ok(result.Response) : Problem(result);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        // Always 204, even for a token that was never valid: logout has nothing to report and
        // nothing to leak.
        await authentication.LogoutAsync(request.RefreshToken, cancellationToken);

        return NoContent();
    }

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private ObjectResult Problem(AuthenticationResult result)
    {
        (int status, string code, string title) = result.Outcome switch
        {
            AuthenticationOutcome.EmailAlreadyRegistered => (
                StatusCodes.Status409Conflict,
                "email_already_registered",
                "That email address is already registered"),
            AuthenticationOutcome.PasswordRejected => (
                StatusCodes.Status422UnprocessableEntity,
                "validation_failed",
                "The password does not meet the policy"),
            AuthenticationOutcome.LockedOut => (
                StatusCodes.Status401Unauthorized,
                "account_locked",
                "Too many failed attempts; the account is locked for a while"),
            _ => (
                StatusCodes.Status401Unauthorized,
                "invalid_credentials",
                "Email or password is incorrect"),
        };

        ProblemDetails problem = new()
        {
            Type = ErrorResponse.TypeFor(code),
            Title = title,
            Status = status,
            // Deliberately the same wording whether the address exists or the password is wrong.
            Detail = title,
            Extensions = { ["code"] = code },
        };

        if (result.Errors.Count > 0)
        {
            problem.Extensions["errors"] = new Dictionary<string, string[]>
            {
                ["password"] = [.. result.Errors],
            };
        }

        return StatusCode(status, problem);
    }
}
