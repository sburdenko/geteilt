using Geteilt.Contracts.Settlements;

namespace Geteilt.Web.Api;

public sealed class GirocodeApi(HttpClient http)
{
    /// <summary>
    /// The code for a transfer nobody has recorded yet, with the fields it was drawn from: the QR
    /// screen shows both, and they have to come from one read.
    /// </summary>
    public Task<Result<GirocodeDto>> PreviewAsync(
        Guid groupId,
        Guid toMemberId,
        decimal amount,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<GirocodeDto>(
            http,
            ApiCall.Post(
                $"api/groups/{groupId}/girocode/preview?format=json",
                new GirocodePreviewRequest(toMemberId, amount)),
            cancellationToken);
}
