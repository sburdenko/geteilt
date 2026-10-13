using Geteilt.Application.Expenses;
using Geteilt.Application.Groups;
using Geteilt.Application.Ports;
using Geteilt.Contracts.Settlements;
using Geteilt.Domain.Expenses;
using Geteilt.Domain.Groups;
using Geteilt.Domain.Settlements;
using Microsoft.EntityFrameworkCore;
using ContractSettlementMethod = Geteilt.Contracts.Settlements.SettlementMethod;
using DomainSettlementMethod = Geteilt.Domain.Settlements.SettlementMethod;

namespace Geteilt.Application.Settlements;

public sealed class SettlementService(IAppDbContext dbContext, TimeProvider timeProvider) : ISettlementService
{
    public async Task<SettlementDto?> CreateAsync(
        Guid userId,
        Guid groupId,
        CreateSettlementRequest request,
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

        await RequireMemberAsync(groupId, request.FromMemberId, cancellationToken);
        await RequireMemberAsync(groupId, request.ToMemberId, cancellationToken);

        if (!string.Equals(request.Currency, group.BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw ExpenseException.ForCurrency(request.Currency, group.BaseCurrency);
        }

        // Amount and the from/to check are the domain's business — Settlement.Create refuses a
        // non-positive amount and a transfer to oneself, and the database repeats both as CHECK
        // constraints.
        Settlement settlement = Settlement.Create(
            Guid.CreateVersion7(),
            groupId,
            request.FromMemberId,
            request.ToMemberId,
            request.Amount,
            request.Currency,
            request.Date,
            timeProvider.GetUtcNow(),
            ToDomain(request.Method),
            request.Note);

        dbContext.Settlements.Add(settlement);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(userId, groupId, settlement.Id, cancellationToken);
    }

    public async Task<SettlementDto?> GetAsync(
        Guid userId,
        Guid groupId,
        Guid settlementId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.VisibleTo(userId).AnyAsync(group => group.Id == groupId, cancellationToken))
        {
            return null;
        }

        return await dbContext.Settlements
            .AsNoTracking()
            .Where(settlement => settlement.GroupId == groupId && settlement.Id == settlementId)
            .Select(Projection())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<SettlementPage?> ListAsync(
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

        int pageSize = Math.Clamp(limit ?? ExpenseService.DefaultPageSize, 1, ExpenseService.MaxPageSize);

        IQueryable<Settlement> page = dbContext.Settlements
            .AsNoTracking()
            .Where(settlement => settlement.GroupId == groupId);

        if (ExpenseCursor.TryDecode(cursor, out ExpenseCursor from))
        {
            page = page.Where(settlement =>
                settlement.Date < from.Date || (settlement.Date == from.Date && settlement.Id < from.Id));
        }

        List<SettlementDto> items = await page
            .OrderByDescending(settlement => settlement.Date)
            .ThenByDescending(settlement => settlement.Id)
            .Take(pageSize + 1)
            .Select(Projection())
            .ToListAsync(cancellationToken);

        string? nextCursor = null;

        if (items.Count > pageSize)
        {
            items.RemoveAt(pageSize);
            SettlementDto last = items[^1];
            nextCursor = new ExpenseCursor(last.Date, last.Id).Encode();
        }

        return new SettlementPage(items, nextCursor);
    }

    /// <summary>Soft delete, and repeatable for the same reason an expense delete is.</summary>
    public async Task<bool> DeleteAsync(
        Guid userId,
        Guid groupId,
        Guid settlementId,
        CancellationToken cancellationToken)
    {
        Group? group = await dbContext.VisibleTo(userId)
            .SingleOrDefaultAsync(candidate => candidate.Id == groupId, cancellationToken);

        if (group is null)
        {
            return false;
        }

        group.RejectIfArchived();

        Settlement? settlement = await dbContext.Settlements
            .SingleOrDefaultAsync(
                candidate => candidate.Id == settlementId && candidate.GroupId == groupId,
                cancellationToken);

        if (settlement is null)
        {
            return await dbContext.Settlements
                .IgnoreQueryFilters()
                .AnyAsync(candidate => candidate.Id == settlementId && candidate.GroupId == groupId, cancellationToken);
        }

        settlement.Delete(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task RequireMemberAsync(Guid groupId, Guid memberId, CancellationToken cancellationToken)
    {
        bool belongs = await dbContext.Members.AnyAsync(
            member => member.GroupId == groupId && member.Id == memberId,
            cancellationToken);

        if (!belongs)
        {
            throw ExpenseException.ForMemberOutsideGroup(memberId);
        }
    }

    private System.Linq.Expressions.Expression<Func<Settlement, SettlementDto>> Projection() =>
        settlement => new SettlementDto(
            settlement.Id,
            settlement.FromMemberId,
            dbContext.Members.Where(member => member.Id == settlement.FromMemberId)
                .Select(member => member.DisplayName).First(),
            settlement.ToMemberId,
            dbContext.Members.Where(member => member.Id == settlement.ToMemberId)
                .Select(member => member.DisplayName).First(),
            settlement.Amount,
            settlement.Currency,
            settlement.Date,
            settlement.Method == DomainSettlementMethod.Cash ? ContractSettlementMethod.Cash
                : settlement.Method == DomainSettlementMethod.Other ? ContractSettlementMethod.Other
                : ContractSettlementMethod.BankTransfer,
            settlement.Note,
            settlement.CreatedAt);

    private static DomainSettlementMethod ToDomain(ContractSettlementMethod method) => method switch
    {
        ContractSettlementMethod.Cash => DomainSettlementMethod.Cash,
        ContractSettlementMethod.Other => DomainSettlementMethod.Other,
        _ => DomainSettlementMethod.BankTransfer,
    };
}
