using System.ComponentModel.DataAnnotations;

namespace Geteilt.Contracts.Settlements;

public sealed record CreateSettlementRequest(
    Guid FromMemberId,
    Guid ToMemberId,
    decimal Amount,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    DateOnly Date,
    SettlementMethod Method,
    [StringLength(200)] string? Note);
