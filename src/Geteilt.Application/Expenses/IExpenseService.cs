using Geteilt.Contracts.Expenses;

namespace Geteilt.Application.Expenses;

public interface IExpenseService
{
    /// <summary>Null when the group is not there for this caller.</summary>
    Task<ExpenseDto?> CreateAsync(
        Guid userId,
        Guid groupId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseDto?> GetAsync(Guid userId, Guid groupId, Guid expenseId, CancellationToken cancellationToken);

    /// <summary>Replaces the expense whole, shares included.</summary>
    Task<ExpenseDto?> UpdateAsync(
        Guid userId,
        Guid groupId,
        Guid expenseId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken);

    /// <summary>True once the expense is gone, including when it already was.</summary>
    Task<bool> DeleteAsync(Guid userId, Guid groupId, Guid expenseId, CancellationToken cancellationToken);

    /// <summary>Newest first, paged by cursor. Null when the group is not there for this caller.</summary>
    Task<ExpensePage?> ListAsync(
        Guid userId,
        Guid groupId,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken);
}
