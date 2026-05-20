namespace PolarSharp.EcommerceStorefronts.Abstractions;

/// <summary>
/// Records the response previously returned for an idempotency-keyed mutation so the
/// same key short-circuits to the original response instead of re-executing the
/// command. Honours the <c>X-Storefront-Idempotency</c> header contract documented on
/// the storefront command DTOs (e.g. <c>AddToCartCommand.IdempotencyToken</c>).
/// </summary>
/// <remarks>
/// The semantics mirror established idempotency-key patterns (Stripe's, etc.):
/// a request that carries a token already seen for the same caller + command kind
/// returns the FIRST response — the second invocation does not re-execute the side
/// effect. This protects against network-blip retries that would otherwise cause
/// double-adds to a cart or duplicate checkout sessions for the same cart.
/// <para>
/// Lift-safe: the abstraction lives in the storefront-core abstractions package; the
/// storefront-core default ships an in-process implementation, and production hosts
/// can replace it with a Redis / distributed-cache-backed implementation by
/// registering their own <c>IStorefrontIdempotencyCache</c> before
/// <c>AddPolarStorefrontsCore</c>.
/// </para>
/// <para>
/// The cache is type-aware (the same key can carry different result types for
/// different command kinds) — callers must use the same <c>TResult</c> when
/// reading and writing under one key. Mismatches resolve as a miss.
/// </para>
/// </remarks>
public interface IStorefrontIdempotencyCache
{
    /// <summary>
    /// Attempts to retrieve a previously-cached response for <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="TResult">The cached response type.</typeparam>
    /// <param name="key">The cache key (typically <c>commandKind|owner|token</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached response when present and not yet expired; otherwise
    /// <see cref="StorefrontOption{T}.None"/>.</returns>
    Task<StorefrontOption<TResult>> TryGetAsync<TResult>(string key, CancellationToken ct);

    /// <summary>Records <paramref name="value"/> against <paramref name="key"/> with the given TTL.</summary>
    /// <typeparam name="TResult">The response type to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The response to cache.</param>
    /// <param name="ttl">Time-to-live; expired entries are treated as absent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the entry is stored.</returns>
    Task SetAsync<TResult>(string key, TResult value, TimeSpan ttl, CancellationToken ct);
}
