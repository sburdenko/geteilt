using Geteilt.Contracts.Members;

namespace Geteilt.Web.Api;

public sealed class MembersApi(HttpClient http)
{
    public Task<Result<IReadOnlyList<MemberDto>>> ListAsync(
        Guid groupId,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<IReadOnlyList<MemberDto>>(
            http, ApiCall.Get($"api/groups/{groupId}/members"), cancellationToken);

    public Task<Result<MemberDto>> AddAsync(
        Guid groupId,
        AddMemberRequest request,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<MemberDto>(
            http, ApiCall.Post($"api/groups/{groupId}/members", request), cancellationToken);
}
