namespace Geteilt.Api.Idempotency;

/// <summary>
/// Marks a POST that does not need an <c>Idempotency-Key</c>.
/// </summary>
/// <remarks>
/// The filter protects every POST by default and this is the way out, rather than the other way
/// round. Forgetting to opt in on a new endpoint would leave it silently unprotected; forgetting
/// to opt out is noticed immediately, because the endpoint stops working.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SkipIdempotencyAttribute : Attribute;
