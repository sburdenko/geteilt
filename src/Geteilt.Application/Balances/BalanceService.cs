using Geteilt.Application.Groups;
using Geteilt.Application.Ports;
using Geteilt.Contracts.Balances;
using Geteilt.Domain.Balances;
using Microsoft.EntityFrameworkCore;

namespace Geteilt.Application.Balances;

public sealed class BalanceService(IAppDbContext dbContext, TimeProvider timeProvider) : IBalanceService
{
    public async Task<BalancesResponse?> GetAsync(Guid userId, Guid groupId, CancellationToken cancellationToken)
    {
        string? baseCurrency = await dbContext.VisibleTo(userId)
            .Where(group => group.Id == groupId)
            .Select(group => group.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken);

        if (baseCurrency is null)
        {
            return null;
        }

        IReadOnlyList<BalanceDto> balances = await CalculateAsync(groupId, cancellationToken);

        return new BalancesResponse(baseCurrency, balances, timeProvider.GetUtcNow());
    }

    public async Task<SettlementPlanResponse?> GetPlanAsync(
        Guid userId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        string? baseCurrency = await dbContext.VisibleTo(userId)
            .Where(group => group.Id == groupId)
            .Select(group => group.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken);

        if (baseCurrency is null)
        {
            return null;
        }

        IReadOnlyList<BalanceDto> balances = await CalculateAsync(groupId, cancellationToken);

        // Computed on demand and stored nowhere. A saved plan would go stale the moment anybody
        // adds an expense, and a stale plan is worse than none — it tells people to send money
        // that is no longer owed.
        IReadOnlyList<SettlementTransfer> transfers = SettlementPlanner.Plan(
            [.. balances.Select(balance => new MemberBalance(balance.MemberId, balance.Balance))]);

        Dictionary<Guid, string> names = balances.ToDictionary(
            balance => balance.MemberId,
            balance => balance.MemberName);

        Dictionary<Guid, string?> ibans = await IbansAsync(groupId, cancellationToken);

        return new SettlementPlanResponse(
            baseCurrency,
            [
                .. transfers.Select(transfer =>
                {
                    string? toIban = ibans.GetValueOrDefault(transfer.ToMemberId);

                    return new PlannedTransferDto(
                        transfer.FromMemberId,
                        names.GetValueOrDefault(transfer.FromMemberId, string.Empty),
                        transfer.ToMemberId,
                        names.GetValueOrDefault(transfer.ToMemberId, string.Empty),
                        transfer.Amount,
                        baseCurrency,
                        toIban,
                        toIban is not null);
                })
            ],
            timeProvider.GetUtcNow());
    }

    /// <summary>
    /// One aggregating query, not five hundred expenses in memory.
    /// </summary>
    /// <remarks>
    /// Every member is listed, including those at zero and those who have left. Dropping anyone
    /// would drop their debts from the sum, and "the balances add up to zero" — the one property
    /// worth checking — would stop holding.
    ///
    /// The signs: paying out lifts a balance exactly as paying for an expense does, so the sender
    /// of a settlement gets a plus and the receiver a minus. The other way round a settlement would
    /// deepen the debt it repays.
    /// </remarks>
    private async Task<IReadOnlyList<BalanceDto>> CalculateAsync(Guid groupId, CancellationToken cancellationToken)
    {
        List<BalanceDto> balances = await dbContext.Members
            .AsNoTracking()
            .Where(member => member.GroupId == groupId)
            .Select(member => new BalanceDto(
                member.Id,
                member.DisplayName,
                (dbContext.Expenses
                    .Where(expense => expense.GroupId == groupId && expense.PaidByMemberId == member.Id)
                    .Sum(expense => (decimal?)expense.AmountInBaseCurrency) ?? 0m)

                - (dbContext.Expenses
                    .Where(expense => expense.GroupId == groupId)
                    .SelectMany(expense => expense.Shares)
                    .Where(share => share.MemberId == member.Id)
                    .Sum(share => (decimal?)share.ShareAmountInBaseCurrency) ?? 0m)

                + (dbContext.Settlements
                    .Where(settlement => settlement.GroupId == groupId && settlement.FromMemberId == member.Id)
                    .Sum(settlement => (decimal?)settlement.AmountInBaseCurrency) ?? 0m)

                - (dbContext.Settlements
                    .Where(settlement => settlement.GroupId == groupId && settlement.ToMemberId == member.Id)
                    .Sum(settlement => (decimal?)settlement.AmountInBaseCurrency) ?? 0m)))
            .ToListAsync(cancellationToken);

        // Ordered by member id as text, the same tie-break Money.Allocate uses, so the answer does
        // not depend on the order the database happened to return rows in.
        return [.. balances.OrderBy(balance => balance.MemberId.ToString(), StringComparer.Ordinal)];
    }

    /// <summary>
    /// The member's own IBAN, falling back to the one on their account. A member added by hand may
    /// have an IBAN typed straight into the group, while somebody with an account usually keeps it
    /// on their profile.
    /// </summary>
    private async Task<Dictionary<Guid, string?>> IbansAsync(Guid groupId, CancellationToken cancellationToken) =>
        await dbContext.Members
            .AsNoTracking()
            .Where(member => member.GroupId == groupId)
            .Select(member => new
            {
                member.Id,
                Iban = member.Iban ?? dbContext.Users
                    .Where(user => user.Id == member.UserId)
                    .Select(user => user.Iban)
                    .FirstOrDefault(),
            })
            .ToDictionaryAsync(row => row.Id, row => row.Iban, cancellationToken);
}
