using Geteilt.Contracts.Groups;

namespace Geteilt.Web.Api;

public sealed class GroupsApi(HttpClient http)
{
    public Task<Result<IReadOnlyList<GroupDto>>> ListAsync(CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<IReadOnlyList<GroupDto>>(http, ApiCall.Get("api/groups"), cancellationToken);

    public Task<Result<GroupDto>> GetAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<GroupDto>(http, ApiCall.Get($"api/groups/{groupId}"), cancellationToken);

    public Task<Result<GroupDto>> CreateAsync(
        CreateGroupRequest request,
        CancellationToken cancellationToken = default) =>
        ApiCall.SendAsync<GroupDto>(http, ApiCall.Post("api/groups", request), cancellationToken);
}
