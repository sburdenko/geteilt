using Geteilt.Application.Groups;
using Geteilt.Application.Ports;
using Geteilt.Contracts.Expenses;
using Geteilt.Domain.Expenses;
using Geteilt.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using ContractSplitMethod = Geteilt.Contracts.Expenses.SplitMethod;
using DomainShareInput = Geteilt.Domain.Expenses.ShareInput;
using DomainSplitMethod = Geteilt.Domain.Expenses.SplitMethod;

namespace Geteilt.Application.Expenses;

public sealed class ExpenseService(IAppDbContext dbContext, TimeProvider timeProvider) : IExpenseService
{
    public async Task<ExpenseDto?> CreateAsync(
        Guid userId,
        Guid groupId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Group? group = await dbContext.VisibleTo(userId)
            .SingleOrDefaultAsync(candidate => candidate.Id == groupId, cancellationToken);

        if (group is null)
        {
            return null;
        }

        group.RejectIfArchived();

        // Everyone in the group, including those who have left: their old expenses still count, so
        // they remain legal participants of a correction to one.
        Guid[] memberIds = await dbContext.Members
            .Where(member => member.GroupId == groupId)
            .Select(member => member.Id)
            .ToArrayAsync(cancellationToken);

        Guid expenseId = Guid.CreateVersion7();
        DateTimeOffset now = timeProvider.GetUtcNow();

        // Whoever recorded it, which is not necessarily whoever paid.
        Guid createdByMemberId = await dbContext.Members
            .Where(member => member.GroupId == groupId && member.UserId == userId)
            .Select(member => member.Id)
            .SingleAsync(cancellationToken);

        // Shares are worked out by the domain and stored explicitly, never recomputed on read:
        // ten euro over three is 3.34 / 3.33 / 3.33, and who gets the extra cent must not change
        // when somebody new joins the group tomorrow.
        IReadOnlyList<ExpenseShare> shares = ShareCalculator.Calculate(
            expenseId,
            request.Amount,
            ToDomain(request.SplitMethod),
            [.. request.Shares.Select(share => ToDomain(share, request.SplitMethod))]);

        Expense expense = Expense.Create(
            new NewExpense
            {
                Id = expenseId,
                GroupId = groupId,
                Description = request.Description,
                PaidByMemberId = request.PaidByMemberId,
                Amount = request.Amount,
                Currency = request.Currency,
                Date = request.Date,
                SplitMethod = ToDomain(request.SplitMethod),
                CreatedByMemberId = createdByMemberId,
                CreatedAt = now,
            },
            shares,
            memberIds,
            group.BaseCurrency);

        // One SaveChanges writes the expense and its shares together: EF wraps them in a single
        // transaction, and an expense whose shares did not make it would break the sum invariant.
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(userId, groupId, expenseId, cancellationToken);
    }

    public async Task<ExpenseDto?> UpdateAsync(
        Guid userId,
        Guid groupId,
        Guid expenseId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Group? group = await dbContext.VisibleTo(userId)
            .SingleOrDefaultAsync(candidate => candidate.Id == groupId, cancellationToken);

        if (group is null)
        {
            return null;
        }

        group.RejectIfArchived();

        // Tracked, and with the shares loaded: they are being replaced, so EF has to know which
        // rows to delete.
        Expense? expense = await dbContext.Expenses
            .Include(candidate => candidate.Shares)
            .SingleOrDefaultAsync(candidate => candidate.Id == expenseId && candidate.GroupId == groupId, cancellationToken);

        if (expense is null)
        {
            return null;
        }

        Guid[] memberIds = await dbContext.Members
            .Where(member => member.GroupId == groupId)
            .Select(member => member.Id)
            .ToArrayAsync(cancellationToken);

        DateTimeOffset now = timeProvider.GetUtcNow();

        expense.Update(
            new NewExpense
            {
                Id = expenseId,
                GroupId = groupId,
                Description = request.Description,
                PaidByMemberId = request.PaidByMemberId,
                Amount = request.Amount,
                Currency = request.Currency,
                Date = request.Date,
                SplitMethod = ToDomain(request.SplitMethod),
                CreatedByMemberId = expense.CreatedByMemberId,
                CreatedAt = expense.CreatedAt,
            },
            ShareCalculator.Calculate(
                expenseId,
                request.Amount,
                ToDomain(request.SplitMethod),
                [.. request.Shares.Select(share => ToDomain(share, request.SplitMethod))]),
            memberIds,
            group.BaseCurrency,
            now);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(userId, groupId, expenseId, cancellationToken);
    }

    /// <summary>
    /// Soft delete. Repeating it answers the same way, because DELETE says "make it not be there"
    /// and after the first call it already is not.
    /// </summary>
    public async Task<bool> DeleteAsync(
        Guid userId,
        Guid groupId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        Group? group = await dbContext.VisibleTo(userId)
            .SingleOrDefaultAsync(candidate => candidate.Id == groupId, cancellationToken);

        if (group is null)
        {
            return false;
        }

        group.RejectIfArchived();

        // Already-deleted rows are hidden by the query filter, so a repeat finds nothing and simply
        // reports success — the caller's intent is satisfied either way.
        Expense? expense = await dbContext.Expenses
            .SingleOrDefaultAsync(candidate => candidate.Id == expenseId && candidate.GroupId == groupId, cancellationToken);

        if (expense is null)
        {
            bool everExisted = await dbContext.Expenses
                .IgnoreQueryFilters()
                .AnyAsync(candidate => candidate.Id == expenseId && candidate.GroupId == groupId, cancellationToken);

            return everExisted;
        }

        expense.Delete(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 100;

    public async Task<ExpensePage?> ListAsync(
        Guid userId,
        Guid groupId,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.VisibleTo(userId).AnyAsync(group => group.Id == groupId, cancellationToken))
        {
            return null;
        }

        int pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);

        IQueryable<Domain.Expenses.Expense> page = dbContext.Expenses
            .AsNoTracking()
            .Where(expense => expense.GroupId == groupId);

        if (ExpenseCursor.TryDecode(cursor, out ExpenseCursor from))
        {
            // Strictly after the last row of the previous page in the ordering (date DESC, id DESC).
            // An unreadable cursor is treated as no cursor: the client gets the first page rather
            // than an error, which is the friendlier answer to a stale bookmark.
            page = page.Where(expense =>
                expense.Date < from.Date || (expense.Date == from.Date && expense.Id < from.Id));
        }

        // One row more than asked for, purely to learn whether another page exists — cheaper than
        // a second COUNT query over the same index.
        List<ExpenseDto> items = await page
            .OrderByDescending(expense => expense.Date)
            .ThenByDescending(expense => expense.Id)
            .Take(pageSize + 1)
            .Select(Projection())
            .ToListAsync(cancellationToken);

        string? nextCursor = null;

        if (items.Count > pageSize)
        {
            items.RemoveAt(pageSize);
            ExpenseDto last = items[^1];
            nextCursor = new ExpenseCursor(last.Date, last.Id).Encode();
        }

        return new ExpensePage(items, nextCursor);
    }

    public async Task<ExpenseDto?> GetAsync(
        Guid userId,
        Guid groupId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.VisibleTo(userId).AnyAsync(group => group.Id == groupId, cancellationToken))
        {
            return null;
        }

        return await dbContext.Expenses
            .AsNoTracking()
            .Where(expense => expense.GroupId == groupId && expense.Id == expenseId)
            .Select(Projection())
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Built once and reused, so the feed and a single read cannot drift apart. An expression, not
    /// a method: EF has to translate it into SQL, and a method call it cannot see inside would send
    /// it back to loading whole rows and mapping them in memory.
    /// </summary>
    private System.Linq.Expressions.Expression<Func<Domain.Expenses.Expense, ExpenseDto>> Projection() =>
        expense => new ExpenseDto(
                expense.Id,
                expense.Description,
                expense.PaidByMemberId,
                dbContext.Members
                    .Where(member => member.Id == expense.PaidByMemberId)
                    .Select(member => member.DisplayName)
                    .First(),
                expense.Amount,
                expense.Currency,
                expense.AmountInBaseCurrency,
                expense.ExchangeRate,
                expense.Date,
                expense.SplitMethod == DomainSplitMethod.Equal ? ContractSplitMethod.Equal
                    : expense.SplitMethod == DomainSplitMethod.Shares ? ContractSplitMethod.Shares
                    : expense.SplitMethod == DomainSplitMethod.Percentages ? ContractSplitMethod.Percentages
                    : ContractSplitMethod.ExactAmounts,
                expense.Shares
                    .Select(share => new ExpenseShareDto(
                        share.MemberId,
                        dbContext.Members
                            .Where(member => member.Id == share.MemberId)
                            .Select(member => member.DisplayName)
                            .First(),
                        share.ShareAmount))
                    .ToList(),
                expense.CreatedAt,
                expense.UpdatedAt);

    /// <summary>
    /// Exact amounts carry a figure per person; shares and percentages carry a weight; an even
    /// split carries neither, because naming the member is the whole input.
    /// </summary>
    private static DomainShareInput ToDomain(ShareInputDto share, ContractSplitMethod method) => new(
        share.MemberId,
        method == ContractSplitMethod.ExactAmounts ? share.Amount : share.Weight);

    private static DomainSplitMethod ToDomain(ContractSplitMethod method) => method switch
    {
        ContractSplitMethod.Equal => DomainSplitMethod.Equal,
        ContractSplitMethod.ExactAmounts => DomainSplitMethod.ExactAmounts,
        ContractSplitMethod.Shares => DomainSplitMethod.Shares,
        ContractSplitMethod.Percentages => DomainSplitMethod.Percentages,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown split method."),
    };
}
