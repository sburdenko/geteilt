namespace Geteilt.Contracts.Expenses;

public sealed record ExpenseShareDto(Guid MemberId, string MemberName, decimal ShareAmount);
