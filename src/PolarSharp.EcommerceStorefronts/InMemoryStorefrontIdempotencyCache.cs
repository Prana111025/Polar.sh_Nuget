using System.Collections.Concurrent;
using PolarSharp.EcommerceStorefronts.Abstractions;

namespace PolarSharp.EcommerceStorefronts;

/// <summary>
/// In-process <see cref="IStorefrontIdempotencyCache"/>. Registered as the storefront-
/// core default; suitable for development and single-process deployments.
/// </summary>
/// <remarks>
/// Production multi-process hosts replace this with a Redis / IDistributedCache-backed
/// implementation so an idempotency-keyed retry that lands on a different server
/// still short-circuits.
/// <para>
/// Expired entries are pruned lazily on access — the cache does not run a background
/// sweeper. For the cart + checkout use cases (small payloads, TTL measured in hours)
/// this is fine; high-throughput deployments swapping to a distributed cache get
/// real TTL enforcement from the cache backend.
/// </para>
/// </remarks>
public sealed class InMemoryStorefrontIdempotencyCache : IStorefrontIdempotencyCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly TimeProvider _clock;

    /// <summary>Initialises the cache.</summary>
    /// <param name="clock">Clock used for expiry computation; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryStorefrontIdempotencyCache(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public Task<StorefrontOption<TResult>> TryGetAsync<TResult>(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        if (!_entries.TryGetValue(key, out var entry))
        {
            return Task.FromResult(StorefrontOption<TResult>.None);
        }
        if (entry.ExpiresAt <= _clock.GetUtcNow())
        {
            _entries.TryRemove(key, out _);
            return Task.FromResult(StorefrontOption<TResult>.None);
        }
        if (entry.Value is TResult typed)
        {
            return Task.FromResult(StorefrontOption<TResult>.Some(typed));
        }
        // Type mismatch — treat as miss rather than throwing; the contract is that
        // callers must use a stable TResult per key.
        return Task.FromResult(StorefrontOption<TResult>.None);
    }

    /// <inheritdoc/>
    public Task SetAsync<TResult>(string key, TResult value, TimeSpan ttl, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ct.ThrowIfCancellationRequested();
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive.");
        }
        _entries[key] = new Entry(value, _clock.GetUtcNow() + ttl);
        return Task.CompletedTask;
    }

    private readonly record struct Entry(object Value, DateTimeOffset ExpiresAt);
}
