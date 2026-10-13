using Geteilt.Contracts.Groups;
using Geteilt.Contracts.Members;
using Geteilt.Web.Api;

namespace Geteilt.Web.Expenses;

/// <summary>
/// The new-expense screen once there is something to fill in: the group's currency, who can take
/// part, and the draft. It exists only when both the group and its members arrived, so there is
/// no currency guessed while one of them is missing.
/// </summary>
/// <param name="Idempotency">
/// Lives as long as the opened form, across every edit. Save pressed again after a lost answer
/// sends the same key when the draft is unchanged, and a new one when it was edited.
/// </param>
public sealed record ExpenseForm(
    string Currency,
    IReadOnlyList<MemberDto> Members,
    ExpenseDraft Draft,
    IdempotencyScope Idempotency)
{
    public static ExpenseForm Open(GroupDto group, IReadOnlyList<MemberDto> members, Guid? viewerUserId, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(members);

        // Someone who has left keeps their old expenses but takes no part in new ones.
        MemberDto[] present = [.. members.Where(member => member.RemovedAt is null)];

        ExpenseDraft draft = ExpenseDraft.ForGroup(today, present.Select(member => member.Id));

        // Whoever is looking at the screen usually just paid. One tap changes it, and a wrong
        // guess is visible in the same control that fixes it.
        MemberDto? viewer = present.FirstOrDefault(member => member.UserId == viewerUserId);

        return new ExpenseForm(
            group.BaseCurrency,
            present,
            viewer is null ? draft : draft.WithPayer(viewer.Id),
            IdempotencyScope.New());
    }

    public ExpenseForm With(ExpenseDraft draft) => this with { Draft = draft };
}
