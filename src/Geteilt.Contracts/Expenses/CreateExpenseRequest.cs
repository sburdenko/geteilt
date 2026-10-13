using System.ComponentModel.DataAnnotations;

namespace Geteilt.Contracts.Expenses;

public sealed record CreateExpenseRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Description,
    Guid PaidByMemberId,
    decimal Amount,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    DateOnly Date,
    SplitMethod SplitMethod,
    [Required, MinLength(1)] IReadOnlyList<ShareInputDto> Shares);
