using System.Net.Http.Json;
using Geteilt.Api.Idempotency;
using Geteilt.Contracts;

namespace Geteilt.Api.Tests;

/// <summary>
/// Sends a POST with a fresh <c>Idempotency-Key</c>, the way a real client does.
/// </summary>
/// <remarks>
/// Every state-changing POST needs one, so tests that are not about idempotency
/// itself should not have to think about it. A new key per call is what a client does too: the key
/// identifies one intent, and reusing it for a different one is the mistake the filter catches.
/// </remarks>
internal static class IdempotentHttpClient
{
    internal static Task<HttpResponseMessage> PostAsync<T>(this HttpClient client, Uri uri, T body)
    {
        HttpRequestMessage request = new(HttpMethod.Post, uri) { Content = JsonContent.Create(body, options: GeteiltJson.Options) };

        request.Headers.Add(IdempotencyFilter.HeaderName, Guid.CreateVersion7().ToString());

        return client.SendAsync(request);
    }
}
