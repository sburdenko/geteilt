using System.Collections.Immutable;
using Geteilt.Contracts.Expenses;
using Geteilt.Domain.Expenses;
using ContractSplitMethod = Geteilt.Contracts.Expenses.SplitMethod;
using DomainSplitMethod = Geteilt.Domain.Expenses.SplitMethod;

namespace Geteilt.Web.Expenses;

/// <summary>
/// An expense as it is being filled in. Every change returns a new draft, so the form has no state
/// that can be half-updated.
/// </summary>
public sealed record ExpenseDraft(
    string Description,
    decimal? Amount,
    Guid? PaidByMemberId,
    DateOnly Date,
    SplitDraft Split)
{
    public const int MaxDescriptionLength = 200;

    private const int AllowedDaysAhead = 1;

    /// <summary>A blank expense dated today, with everyone taking part.</summary>
    public static ExpenseDraft ForGroup(DateOnly today, IEnumerable<Guid> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        return new ExpenseDraft(
            string.Empty, null, null, today, new SplitDraft.Evenly(members.ToImmutableHashSet()));
    }

    public ExpenseDraft WithDescription(string description) => this with { Description = description };

    public ExpenseDraft WithAmount(decimal? amount) => this with { Amount = amount };

    public ExpenseDraft WithPayer(Guid memberId) => this with { PaidByMemberId = memberId };

    public ExpenseDraft WithDate(DateOnly date) => this with { Date = date };

    /// <summary>Takes somebody in or out of the split, in whichever mode the split is in.</summary>
    public ExpenseDraft Toggle(Guid memberId) => this with
    {
        Split = Split switch
        {
            SplitDraft.Evenly evenly => new SplitDraft.Evenly(Flip(evenly.Participants, memberId)),
            SplitDraft.Exactly exactly => new SplitDraft.Exactly(Flip(exactly.Amounts, memberId)),
            _ => Split,
        },
    };

    private static IImmutableSet<Guid> Flip(IImmutableSet<Guid> participants, Guid memberId)
    {
        IImmutableSet<Guid> without = participants.Remove(memberId);

        return without.Count == participants.Count ? participants.Add(memberId) : without;
    }

    private static IImmutableDictionary<Guid, decimal> Flip(
        IImmutableDictionary<Guid, decimal> amounts,
        Guid memberId)
    {
        IImmutableDictionary<Guid, decimal> without = amounts.Remove(memberId);

        return without.Count == amounts.Count ? amounts.Add(memberId, 0m) : without;
    }

    /// <summary>
    /// Places a figure against one person. Typing into an even split turns it into an exact one,
    /// seeded with the shares that were on screen — so the first keystroke changes one number
    /// rather than clearing the rest.
    /// </summary>
    public ExpenseDraft WithExactAmount(Guid memberId, decimal amount)
    {
        IImmutableDictionary<Guid, decimal> seed = Split switch
        {
            SplitDraft.Exactly exactly => exactly.Amounts,
            _ => Preview().Shares.ToImmutableDictionary(share => share.MemberId, share => share.Amount),
        };

        return this with { Split = new SplitDraft.Exactly(seed.SetItem(memberId, amount)) };
    }

    /// <summary>Back to an even split over whoever is currently taking part.</summary>
    public ExpenseDraft SplitEvenly() => this with { Split = new SplitDraft.Evenly(Split.Participants) };

    /// <summary>
    /// What each person owes right now. Computed by the same <see cref="ShareCalculator"/> the
    /// server uses, so the figures on screen are the figures that will be stored — down
    /// to which person carries the odd cent.
    /// </summary>
    public SplitPreview Preview()
    {
        Guid[] participants = [.. Split.Participants.OrderBy(id => id.ToString(), StringComparer.Ordinal)];

        if (Split is SplitDraft.Exactly exactly)
        {
            decimal placed = participants.Sum(id => exactly.Amounts[id]);

            decimal left = (Amount ?? 0m) - placed;

            return new SplitPreview(
                [.. participants.Select(id => new MemberShare(id, exactly.Amounts[id], CarriesTheOddCent: false))],
                left,
                AddsUp: Amount is > 0m && left == 0m);
        }

        if (Amount is not { } total || total <= 0m || participants.Length == 0)
        {
            return new SplitPreview(
                [.. participants.Select(id => new MemberShare(id, 0m, false))], Amount ?? 0m, AddsUp: false);
        }

        IReadOnlyList<ExpenseShare> shares = ShareCalculator.Calculate(
            Guid.Empty, total, DomainSplitMethod.Equal, [.. participants.Select(id => new ShareInput(id))]);

        decimal smallest = shares.Min(share => share.ShareAmount);

        return new SplitPreview(
            [.. shares.Select(share => new MemberShare(
                share.MemberId, share.ShareAmount, share.ShareAmount > smallest))],
            0m,
            AddsUp: true);
    }

    /// <summary>
    /// Everything wrong with the draft, empty when it can be sent.
    /// </summary>
    /// <remarks>
    /// These are the server's own rules, kept in the same order and with the same thresholds
    /// (<c>Expense.Create</c>, <c>ShareCalculator</c>). The point is not to spare the server a
    /// request — it re-checks regardless, and has to. It is that a form which lets you press Save
    /// and then refuses is a form that wasted the typing.
    /// </remarks>
    public IReadOnlyList<DraftProblem> Problems(DateOnly today)
    {
        List<DraftProblem> problems = [];

        if (string.IsNullOrWhiteSpace(Description))
        {
            problems.Add(DraftProblem.DescriptionMissing);
        }
        else if (Description.Trim().Length > MaxDescriptionLength)
        {
            problems.Add(DraftProblem.DescriptionTooLong);
        }

        if (Amount is not > 0m)
        {
            problems.Add(DraftProblem.AmountMustBePositive);
        }

        if (PaidByMemberId is null)
        {
            problems.Add(DraftProblem.PayerMissing);
        }

        if (Split.Participants.Count == 0)
        {
            problems.Add(DraftProblem.NobodyTakingPart);
        }
        else if (Split is SplitDraft.Exactly && !Preview().AddsUp)
        {
            problems.Add(DraftProblem.SharesSumMismatch);
        }

        // The server allows one day ahead, for somebody entering last night's dinner in a
        // timezone the server does not share.
        if (Date > today.AddDays(AllowedDaysAhead))
        {
            problems.Add(DraftProblem.DateInFuture);
        }

        return problems;
    }

    public bool CanBeSent(DateOnly today) => Problems(today).Count == 0;

    public CreateExpenseRequest ToRequest(string currency)
    {
        SplitPreview preview = Preview();

        return new CreateExpenseRequest(
            Description.Trim(),
            PaidByMemberId!.Value,
            Amount!.Value,
            currency,
            Date,
            Split is SplitDraft.Exactly ? ContractSplitMethod.ExactAmounts : ContractSplitMethod.Equal,
            [.. preview.Shares.Select(share => new ShareInputDto(
                share.MemberId,
                Split is SplitDraft.Exactly ? share.Amount : null,
                null))]);
    }
}
