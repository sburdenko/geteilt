using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Geteilt.Contracts;
using Geteilt.Contracts.Errors;

namespace Geteilt.Web.Api;

/// <summary>
/// Turns an HTTP exchange into a <see cref="Result{T}"/>. Nothing here throws for a failed call:
/// a server that is down, a 422 and a body that will not parse are all outcomes the UI has to
/// render, not exceptions for someone further up to forget.
/// </summary>
internal static class ApiCall
{
    internal static HttpRequestMessage Get(string path) =>
        new(HttpMethod.Get, new Uri(path, UriKind.Relative));

    internal static HttpRequestMessage Delete(string path) =>
        new(HttpMethod.Delete, new Uri(path, UriKind.Relative));

    internal static HttpRequestMessage Post<TBody>(string path, TBody body) =>
        Post(path, body, Guid.CreateVersion7());

    /// <param name="idempotencyKey">
    /// Kept by the caller when a person may press the same button again after an answer got lost:
    /// the repeat is then the same request, and the server answers it from memory.
    /// </param>
    internal static HttpRequestMessage Post<TBody>(string path, TBody body, Guid idempotencyKey) =>
        Keyed(path, JsonContent.Create(body, options: GeteiltJson.Options), idempotencyKey);

    /// <summary>
    /// For a form whose body can change between presses. The key is taken from the very bytes
    /// that are sent, so an unchanged body repeats its key and a changed one never reuses it.
    /// </summary>
    internal static HttpRequestMessage Post<TBody>(string path, TBody body, IdempotencyScope scope)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(body, GeteiltJson.Options);

        ByteArrayContent content = new(json);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        return Keyed(path, content, scope.KeyFor(json));
    }

    private static HttpRequestMessage Keyed(string path, HttpContent content, Guid idempotencyKey)
    {
        HttpRequestMessage request = new(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = content };

        // Every authenticated POST needs one. The 401 retry inside
        // AuthTokenHandler re-sends this same key, so a renewal cannot double-post.
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());

        return request;
    }

    internal static Task<Result<T>> SendAsync<T>(
        HttpClient http,
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        GuardAsync<T>(async () =>
        {
            using HttpRequestMessage owned = request;
            using HttpResponseMessage response = await http.SendAsync(owned, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new Result<T>.Failed(await ProblemAsync(response, cancellationToken));
            }

            T? value = await response.Content.ReadFromJsonAsync<T>(GeteiltJson.Options, cancellationToken);

            return value is null
                ? new Result<T>.Failed(ApiError.Unreadable((int)response.StatusCode, "Leere Antwort"))
                : new Result<T>.Ok(value);
        });

    internal static Task<Result<Unit>> SendAsync(
        HttpClient http,
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        GuardAsync<Unit>(async () =>
        {
            using HttpRequestMessage owned = request;
            using HttpResponseMessage response = await http.SendAsync(owned, cancellationToken);

            return response.IsSuccessStatusCode
                ? new Result<Unit>.Ok(Unit.Value)
                : new Result<Unit>.Failed(await ProblemAsync(response, cancellationToken));
        });

    private static async Task<Result<T>> GuardAsync<T>(Func<Task<Result<T>>> call)
    {
        try
        {
            return await call();
        }
        catch (HttpRequestException exception)
        {
            return new Result<T>.Failed(ApiError.Unreachable(exception.Message));
        }
        catch (TaskCanceledException exception)
        {
            return new Result<T>.Failed(ApiError.Unreachable(exception.Message));
        }
        catch (JsonException exception)
        {
            return new Result<T>.Failed(ApiError.Unreadable(0, exception.Message));
        }
    }

    private static async Task<ApiError> ProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        int status = (int)response.StatusCode;

        try
        {
            ProblemResponse? problem = await response.Content
                .ReadFromJsonAsync<ProblemResponse>(GeteiltJson.Options, cancellationToken);

            return problem is null
                ? ApiError.Unreadable(status, response.ReasonPhrase)
                : ApiError.From(status, problem);
        }
        catch (JsonException)
        {
            // A proxy or the dev server answering with HTML. The status is still the truth.
            return ApiError.Unreadable(status, response.ReasonPhrase);
        }
        catch (NotSupportedException)
        {
            return ApiError.Unreadable(status, response.ReasonPhrase);
        }
    }
}
