using System.ComponentModel.DataAnnotations;

namespace Geteilt.Contracts.Groups;

public sealed record CreateGroupRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, StringLength(3, MinimumLength = 3)] string BaseCurrency);
