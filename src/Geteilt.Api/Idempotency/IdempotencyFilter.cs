using System.Security.Cryptography;
using System.Text.Json;
using Geteilt.Api.Authentication;
using Geteilt.Application.Ports;
using Geteilt.Domain.Idempotency;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Geteilt.Api.Idempotency;

/// <summary>
/// Makes a repeated POST harmless.
/// </summary>
/// <remarks>
/// A resource filter rather than an action filter, because the hash has to be taken from the raw
/// request bytes — before model binding turns them into an object. Two bodies that deserialise to
/// the same object are still two different requests, and hashing the object would call them equal.
/// </remarks>
public sealed class IdempotencyFilter(
    IAppDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<JsonOptions> jsonOptions) : IAsyncResourceFilter
{
    internal const string HeaderName = "Idempotency-Key";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!Applies(context))
        {
            await next();
            return;
        }

        string key = ReadKey(context);
        Guid userId = context.HttpContext.User.CurrentUserId();
        string endpoint = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
        string hash = await HashBodyAsync(context.HttpContext.Request);

        IdempotencyRecord? seen = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.UserId == userId && record.Key == key,
                context.HttpContext.RequestAborted);

        if (seen is not null)
        {
            if (!string.Equals(seen.RequestHash, hash, StringComparison.Ordinal))
            {
                throw IdempotencyException.Reused(key);
            }

            // Same key, same body: hand back what was answered the first time and run nothing.
            context.Result = new ContentResult
            {
                StatusCode = seen.ResponseStatusCode,
                Content = seen.ResponseBodyJson,
                ContentType = "application/json",
            };

            return;
        }

        ResourceExecutedContext executed = await next();

        await RememberAsync(executed, userId, key, endpoint, hash);
    }

    private static bool Applies(ResourceExecutingContext context) =>
        HttpMethods.IsPost(context.HttpContext.Request.Method)
        && context.HttpContext.User.Identity?.IsAuthenticated == true
        && context.ActionDescriptor.EndpointMetadata.OfType<SkipIdempotencyAttribute>().Any() is false;

    private static string ReadKey(ResourceExecutingContext context)
    {
        string? key = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(key) || key.Length > IdempotencyRecord.MaxKeyLength)
        {
            throw IdempotencyException.Missing();
        }

        return key;
    }

    private static async Task<string> HashBodyAsync(HttpRequest request)
    {
        // Buffering has to be switched on so the body can be read here and again by model binding.
        request.EnableBuffering();
        request.Body.Position = 0;

        byte[] digest = await SHA256.HashDataAsync(request.Body, request.HttpContext.RequestAborted);

        request.Body.Position = 0;

        return Convert.ToHexString(digest);
    }

    private async Task RememberAsync(
        ResourceExecutedContext executed,
        Guid userId,
        string key,
        string endpoint,
        string hash)
    {
        // Only successful answers are remembered. Replaying a failure would freeze a transient
        // problem into a permanent one: the retry that was meant to fix it would get the old error.
        if (executed.Exception is not null || executed.Result is not ObjectResult { Value: not null } result)
        {
            return;
        }

        int status = result.StatusCode ?? StatusCodes.Status200OK;

        if (status is < 200 or > 299)
        {
            return;
        }

        dbContext.IdempotencyRecords.Add(new IdempotencyRecord(
            key,
            userId,
            endpoint,
            hash,
            status,
            JsonSerializer.Serialize(result.Value, jsonOptions.Value.JsonSerializerOptions),
            timeProvider.GetUtcNow()));

        await dbContext.SaveChangesAsync(executed.HttpContext.RequestAborted);
    }
}
