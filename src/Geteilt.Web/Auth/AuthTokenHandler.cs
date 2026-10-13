using System.Net;
using System.Net.Http.Headers;

namespace Geteilt.Web.Auth;

/// <summary>
/// Puts the access token on every request and renews it without the person noticing
///. The renewal itself is <see cref="SessionRenewer"/>'s job.
/// </summary>
public sealed class AuthTokenHandler(AuthState auth, SessionRenewer renewer) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        AuthSession? session = auth.Session;

        if (session is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        if (renewer.DueForRenewal(session))
        {
            session = await renewer.RenewAsync(session, cancellationToken);

            if (session is null)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
            }
        }

        // Buffered before the first send: after a 401 the same body goes out again, and stream
        // content can only be read once.
        byte[]? body = await BufferAsync(request, cancellationToken);

        Authorize(request, session.AccessToken);
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Our clock said the token was good and the server disagreed. One renewal, one retry; a
        // second 401 is an honest answer and the caller gets to see it.
        AuthSession? renewed = await renewer.RenewAsync(session, cancellationToken);

        if (renewed is null)
        {
            return response;
        }

        response.Dispose();

        using HttpRequestMessage retry = Clone(request, body);
        Authorize(retry, renewed.AccessToken);

        return await base.SendAsync(retry, cancellationToken);
    }

    private static void Authorize(HttpRequestMessage request, string accessToken) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    private static async Task<byte[]?> BufferAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

    private static HttpRequestMessage Clone(HttpRequestMessage original, byte[]? body)
    {
        HttpRequestMessage copy = new(original.Method, original.RequestUri) { Version = original.Version };

        // Carries the Idempotency-Key across, so the retry counts as the same operation.
        foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (body is null)
        {
            return copy;
        }

        copy.Content = new ByteArrayContent(body);

        // Content-Type travels on the content, not the request, and the API rejects a body without it.
        foreach (KeyValuePair<string, IEnumerable<string>> header in original.Content!.Headers)
        {
            copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return copy;
    }
}
