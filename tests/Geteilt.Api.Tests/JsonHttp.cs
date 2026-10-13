using System.Net.Http.Json;
using Geteilt.Contracts;

namespace Geteilt.Api.Tests;

/// <summary>
/// HTTP helpers that speak the API's own JSON dialect.
/// </summary>
/// <remarks>
/// The BCL overloads without options use plain web defaults, which do not know that enums travel
/// by name. Tests using those would disagree with the server the same way a real client would —
/// which is exactly the bug a manual curl run found while the whole suite was green.
/// </remarks>
internal static class JsonHttp
{
    internal static Task<T?> ReadAsync<T>(this HttpContent content) =>
        content.ReadFromJsonAsync<T>(GeteiltJson.Options);

    internal static async Task<T?> GetAsync<T>(this HttpClient client, Uri uri)
    {
        HttpResponseMessage response = await client.GetAsync(uri);

        return await response.Content.ReadAsync<T>();
    }

    internal static Task<HttpResponseMessage> PatchAsync<T>(this HttpClient client, Uri uri, T body) =>
        client.PatchAsync(uri, JsonContent.Create(body, options: GeteiltJson.Options));

    internal static Task<HttpResponseMessage> PutAsync<T>(this HttpClient client, Uri uri, T body) =>
        client.PutAsync(uri, JsonContent.Create(body, options: GeteiltJson.Options));
}
