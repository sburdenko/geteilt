using System.ComponentModel.DataAnnotations;

namespace Geteilt.Contracts.Members;

/// <param name="Email">
/// Optional. Given a known address the new member is linked to that account; without one they take
/// part without an account, which is the common case for the friend who never installs anything.
/// </param>
public sealed record AddMemberRequest(
    [Required, StringLength(100, MinimumLength = 1)] string DisplayName,
    [EmailAddress] string? Email,
    string? Iban);
