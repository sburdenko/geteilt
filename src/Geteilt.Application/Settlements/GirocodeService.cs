using Geteilt.Application.Groups;
using Geteilt.Application.Ports;
using Geteilt.Domain.Expenses;
using Geteilt.Domain.Girocode;
using Microsoft.EntityFrameworkCore;

namespace Geteilt.Application.Settlements;

/// <summary>
/// Assembles what a Girocode needs. The payload itself is built by the domain, and the image by
/// Infrastructure — this only decides who is being paid, how much, and out of which group.
/// </summary>
public sealed class GirocodeService(IAppDbContext dbContext) : IGirocodeService
{
    public async Task<GirocodeRequest?> ForSettlementAsync(
        Guid userId,
        Guid settlementId,
        CancellationToken cancellationToken)
    {
        var found = await dbContext.Settlements
            .AsNoTracking()
            .Where(settlement => settlement.Id == settlementId)
            .Where(settlement => dbContext.VisibleTo(userId).Any(group => group.Id == settlement.GroupId))
            .Select(settlement => new
            {
                settlement.GroupId,
                settlement.ToMemberId,
                settlement.Amount,
                settlement.Currency,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return null;
        }

        return await BuildAsync(found.GroupId, found.ToMemberId, found.Amount, found.Currency, cancellationToken);
    }

    public async Task<GirocodeRequest?> ForPreviewAsync(
        Guid userId,
        Guid groupId,
        Guid toMemberId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        string? baseCurrency = await dbContext.VisibleTo(userId)
            .Where(group => group.Id == groupId)
            .Select(group => group.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken);

        return baseCurrency is null
            ? null
            : await BuildAsync(groupId, toMemberId, amount, baseCurrency, cancellationToken);
    }

    private async Task<GirocodeRequest> BuildAsync(
        Guid groupId,
        Guid toMemberId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        var beneficiary = await dbContext.Members
            .AsNoTracking()
            .Where(member => member.Id == toMemberId && member.GroupId == groupId)
            .Select(member => new
            {
                member.DisplayName,
                // The member's own IBAN first, then the one on their account: somebody added by
                // hand may have one typed into the group, somebody with an account keeps it there.
                Iban = member.Iban ?? dbContext.Users
                    .Where(user => user.Id == member.UserId)
                    .Select(user => user.Iban)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (beneficiary is null)
        {
            throw ExpenseException.ForMemberOutsideGroup(toMemberId);
        }

        if (string.IsNullOrWhiteSpace(beneficiary.Iban))
        {
            throw EpcQrException.ForMissingIban();
        }

        string groupName = await dbContext.Groups
            .Where(group => group.Id == groupId)
            .Select(group => group.Name)
            .SingleAsync(cancellationToken);

        return new GirocodeRequest
        {
            BeneficiaryName = beneficiary.DisplayName,
            Iban = beneficiary.Iban,
            Amount = amount,
            Currency = currency,
            // Generated, never typed: "Geteilt: <group>" trimmed to the 140 bytes EPC069-12 allows.
            Remittance = EpcQrPayload.RemittanceFor(groupName),
        };
    }
}
