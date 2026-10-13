using System.Net;
using Geteilt.Contracts.Users;
using Geteilt.Web.Auth;

namespace Geteilt.Web.Tests;

/// <summary>Answers whatever the test says, and records what it was asked.</summary>
internal sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> answer) : HttpMessageHandler
{
    private readonly List<Recorded> _seen = [];

    public IReadOnlyList<Recorded> Seen => _seen;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        _seen.Add(new Recorded(
            request.Headers.Authorization?.Parameter,
            request.Headers.TryGetValues("Idempotency-Key", out IEnumerable<string>? keys)
                ? keys.Single()
                : null,
            body,
            request.Content?.Headers.ContentType?.MediaType));

        return answer(request, _seen.Count - 1);
    }

    internal sealed record Recorded(string? BearerToken, string? IdempotencyKey, string? Body, string? ContentType);
}

internal sealed class InMemoryTokenStorage : ITokenStorage
{
    public AuthSession? Stored { get; set; }

    public int Writes { get; private set; }

    public int Clears { get; private set; }

    public ValueTask<AuthSession?> ReadAsync() => ValueTask.FromResult(Stored);

    public ValueTask WriteAsync(AuthSession session)
    {
        Stored = session;
        Writes++;

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync()
    {
        Stored = null;
        Clears++;

        return ValueTask.CompletedTask;
    }
}

internal sealed class CountingRefresher(Func<string, AuthSession?> exchange) : ITokenRefresher
{
    private readonly List<string> _spent = [];

    public IReadOnlyList<string> Spent => _spent;

    /// <summary>Set to hold every caller until released, so a race can be arranged on purpose.</summary>
    public TaskCompletionSource? Gate { get; init; }

    public async Task<AuthSession?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        _spent.Add(refreshToken);

        if (Gate is not null)
        {
            await Gate.Task;
        }

        return exchange(refreshToken);
    }
}

internal static class Sessions
{
    internal static readonly UserDto Anna = new(
        Guid.Parse("0198f000-0000-7000-8000-000000000001"), "anna@example.com", "Anna", null, "EUR");

    internal static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    internal static AuthSession At(string suffix, TimeSpan accessValidFor) => new(
        $"access-{suffix}",
        Now + accessValidFor,
        $"refresh-{suffix}",
        Now + TimeSpan.FromDays(30),
        Anna);

    internal static HttpResponseMessage Status(HttpStatusCode code) => new(code);
}
