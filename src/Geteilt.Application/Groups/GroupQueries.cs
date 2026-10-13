using Geteilt.Application.Ports;
using Geteilt.Contracts.Groups;
using Geteilt.Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace Geteilt.Application.Groups;

/// <summary>
/// The one place a group query starts. Every read and every write goes through
/// <see cref="VisibleTo"/> first.
/// </summary>
internal static class GroupQueries
{
    /// <summary>
    /// Groups the caller is actually a member of.
    /// </summary>
    /// <remarks>
    /// Authorisation by narrowing the query rather than by an <c>if</c> in the controller
    ///. A check can be forgotten in one method out of twenty and the tests will not
    /// notice; a missing narrowing cannot be forgotten, because without it the query returns
    /// nothing to work with. It also gives 404 for someone else's group for free: the row simply
    /// is not in the result, and answering 403 would confirm that the group exists.
    /// </remarks>
    internal static IQueryable<Group> VisibleTo(this IAppDbContext dbContext, Guid userId) =>
        dbContext.Groups.Where(group => dbContext.Members.Any(member =>
            member.GroupId == group.Id
            && member.UserId == userId
            && member.RemovedAt == null));

    /// <summary>
    /// Projects to the response, computing the balance in the database rather than pulling every
    /// expense into memory.
    /// </summary>
    internal static IQueryable<GroupDto> ToDto(this IQueryable<Group> groups, IAppDbContext dbContext, Guid userId) =>
        groups.Select(group => new GroupDto(
            group.Id,
            group.Name,
            group.BaseCurrency,
            group.CreatedAt,
            group.ArchivedAt,
            dbContext.Members.Count(member => member.GroupId == group.Id && member.RemovedAt == null),

            // Paying out lifts the balance exactly as paying for an expense does — the sender of a
            // settlement gets a plus, the receiver a minus. With those two the other way round a
            // settlement would deepen the very debt it repays.
            //
            // Every term is parenthesised because ?? binds looser than + and -: without the
            // brackets "paid ?? 0 - owed ?? 0" parses as "paid ?? ((0 - owed) ?? ...)" and the
            // whole balance collapses into whatever the first sum returned.
            (dbContext.Expenses
                .Where(expense => expense.GroupId == group.Id
                    && dbContext.Members.Any(member =>
                        member.Id == expense.PaidByMemberId && member.UserId == userId))
                .Sum(expense => (decimal?)expense.AmountInBaseCurrency) ?? 0m)

            - (dbContext.Expenses
                .Where(expense => expense.GroupId == group.Id)
                .SelectMany(expense => expense.Shares)
                .Where(share => dbContext.Members.Any(member =>
                    member.Id == share.MemberId && member.UserId == userId))
                .Sum(share => (decimal?)share.ShareAmountInBaseCurrency) ?? 0m)

            + (dbContext.Settlements
                .Where(settlement => settlement.GroupId == group.Id
                    && dbContext.Members.Any(member =>
                        member.Id == settlement.FromMemberId && member.UserId == userId))
                .Sum(settlement => (decimal?)settlement.AmountInBaseCurrency) ?? 0m)

            - (dbContext.Settlements
                .Where(settlement => settlement.GroupId == group.Id
                    && dbContext.Members.Any(member =>
                        member.Id == settlement.ToMemberId && member.UserId == userId))
                .Sum(settlement => (decimal?)settlement.AmountInBaseCurrency) ?? 0m)));
}
