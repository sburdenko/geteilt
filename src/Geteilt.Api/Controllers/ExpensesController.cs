using Geteilt.Api.Authentication;
using Geteilt.Application.Expenses;
using Geteilt.Contracts.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Geteilt.Api.Controllers;

[ApiController]
[Route("api/groups/{groupId:guid}/expenses")]
[Authorize]
public sealed class ExpensesController(IExpenseService expenses) : ControllerBase
{
    /// <summary>Newest first. Hand <c>cursor</c> back untouched to walk to the next page.</summary>
    [HttpGet]
    [ProducesResponseType<ExpensePage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpensePage>> List(
        Guid groupId,
        CancellationToken cancellationToken,
        [FromQuery] string? cursor = null,
        [FromQuery] int? limit = null)
    {
        ExpensePage? page = await expenses.ListAsync(
            User.CurrentUserId(), groupId, cursor, limit, cancellationToken);

        return page is null ? NotFound() : Ok(page);
    }

    [HttpGet("{expenseId:guid}")]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseDto>> Get(
        Guid groupId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        ExpenseDto? expense = await expenses.GetAsync(User.CurrentUserId(), groupId, expenseId, cancellationToken);

        return expense is null ? NotFound() : Ok(expense);
    }

    [HttpPost]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExpenseDto>> Create(
        Guid groupId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        ExpenseDto? expense = await expenses.CreateAsync(
            User.CurrentUserId(), groupId, request, cancellationToken);

        return expense is null
            ? NotFound()
            : CreatedAtAction(nameof(Get), new { groupId, expenseId = expense.Id }, expense);
    }

    /// <summary>Replaces the expense whole; the shares are recomputed from scratch.</summary>
    [HttpPut("{expenseId:guid}")]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExpenseDto>> Update(
        Guid groupId,
        Guid expenseId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        ExpenseDto? expense = await expenses.UpdateAsync(
            User.CurrentUserId(), groupId, expenseId, request, cancellationToken);

        return expense is null ? NotFound() : Ok(expense);
    }

    /// <summary>Soft delete, and repeatable: a second call answers 204 as well.</summary>
    [HttpDelete("{expenseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid groupId, Guid expenseId, CancellationToken cancellationToken)
    {
        bool deleted = await expenses.DeleteAsync(User.CurrentUserId(), groupId, expenseId, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
