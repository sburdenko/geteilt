using Geteilt.Contracts.Balances;
using Geteilt.Contracts.Settlements;
using Geteilt.Web.Api;
using Geteilt.Web.Loading;

namespace Geteilt.Web.Settlements;

/// <summary>
/// What the QR screen does, kept out of the page so it can be tested without a browser: find the
/// transfer, fetch its code, record it as paid.
/// </summary>
public sealed class SettleFlow(BalancesApi balances, GirocodeApi girocodes, SettlementsApi settlements)
{
    public async Task<Loadable<SettleView>> LoadAsync(
        Guid groupId,
        Guid fromMemberId,
        Guid toMemberId,
        CancellationToken cancellationToken = default)
    {
        Result<SettlementPlanResponse> plan = await balances.PlanAsync(groupId, cancellationToken);

        return await plan.Match(
            found => OpenAsync(groupId, OpenTransfer.Find(found, fromMemberId, toMemberId), cancellationToken),
            error => Task.FromResult<Loadable<SettleView>>(new Loadable<SettleView>.Failed(error)));
    }

    public Task<Result<SettlementDto>> RecordAsync(
        Guid groupId,
        OpenTransfer open,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(open);

        PlannedTransferDto transfer = open.Transfer;
        CreateSettlementRequest request = new(
            transfer.FromMemberId,
            transfer.ToMemberId,
            open.Code.Amount,
            open.Code.Currency,
            today,
            SettlementMethod.BankTransfer,
            Note: null);

        return settlements.CreateAsync(groupId, request, open.IdempotencyKey, cancellationToken);
    }

    private async Task<Loadable<SettleView>> OpenAsync(
        Guid groupId,
        PlannedTransferDto? transfer,
        CancellationToken cancellationToken)
    {
        if (transfer is null)
        {
            return new Loadable<SettleView>.Loaded(new SettleView.NothingOpen());
        }

        Result<GirocodeDto> code = await girocodes.PreviewAsync(
            groupId, transfer.ToMemberId, transfer.Amount, cancellationToken);

        return code.Match<Loadable<SettleView>>(
            found => new Loadable<SettleView>.Loaded(new SettleView.Open(OpenTransfer.Create(transfer, found))),
            error => new Loadable<SettleView>.Failed(error));
    }
}
