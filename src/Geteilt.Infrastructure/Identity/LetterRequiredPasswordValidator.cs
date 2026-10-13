using Microsoft.AspNetCore.Identity;

namespace Geteilt.Infrastructure.Identity;

/// <summary>
/// Requires at least one letter. The policy is "a digit and a letter" with
/// the mixed-case rule, and Identity has no built-in option for that: turning on RequireLowercase
/// would be a case rule by another name, and leaving it off lets ten digits through.
/// </summary>
public sealed class LetterRequiredPasswordValidator : IPasswordValidator<AppUser>
{
    public const string ErrorCode = "PasswordRequiresLetter";

    public Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password)
    {
        if (password is not null && password.Any(char.IsLetter))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = ErrorCode,
            Description = "Passwords must contain at least one letter.",
        }));
    }
}
