using Geteilt.Contracts.Expenses;

namespace Geteilt.Web.Api;

public sealed class ExpensesApi(HttpClient http)
{
    /// <param name="cursor">
    /// Straight back from the previous page, untouched. It is opaque by contract, so the client
    /// neither builds nor reads one.
    /// </param>
    public Task<Result<ExpensePage>> ListAsync(
        Guid groupId,
        string? cursor = null,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<ExpensePage>(
            http,
            ApiCall.Get($"api/groups/{groupId}/expenses{Query(cursor)}"),
            cancellationToken);

    public Task<Result<ExpenseDto>> CreateAsync(
        Guid groupId,
        CreateExpenseRequest request,
        IdempotencyScope idempotency,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<ExpenseDto>(
            http, ApiCall.Post($"api/groups/{groupId}/expenses", request, idempotency), cancellationToken);

    public Task<Result<Unit>> DeleteAsync(
        Guid groupId,
        Guid expenseId,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync(
            http, ApiCall.Delete($"api/groups/{groupId}/expenses/{expenseId}"), cancellationToken);

    private static string Query(string? cursor) =>
        string.IsNullOrEmpty(cursor) ? string.Empty : $"?cursor={Uri.EscapeDataString(cursor)}";
}
