using PolarSharp.PrepaidWallets.Abstractions.Events;

namespace PolarSharp.PrepaidWallets.Abstractions.Stores;

/// <summary>
/// Persistence boundary for wallet events. Implementations are pluggable — Marten (Postgres),
/// EF Core (SQL Server / SQLite / PostgreSQL / MariaDB / Cosmos), or an in-memory store for
/// unit tests.
/// </summary>
/// <remarks>
/// <para>
/// Optimistic concurrency is enforced at append time: writers pass the expected current version
/// (the sequence number of the last event they read); appends throw
/// <see cref="WalletConcurrencyConflictException"/> when another writer has advanced the stream
/// in the meantime.
/// </para>
/// <para>
/// Idempotency is enforced at append time: appending an event whose
/// <see cref="IWalletEvent.IdempotencyKey"/> already exists on this wallet returns the previously
/// recorded event rather than appending a duplicate. The exact semantics are
/// implementation-specific but every implementation respects the wallet's idempotency contract.
/// </para>
/// </remarks>
public interface IWalletEventStore
{
    /// <summary>Load the full event stream for a wallet, starting at the given sequence number (inclusive).</summary>
    /// <param name="walletId">The wallet identifier.</param>
    /// <param name="fromSequenceNoInclusive">First sequence number to include (1 to read from the beginning).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The events in stream order. Empty when the wallet does not exist.</returns>
    Task<IReadOnlyList<IWalletEvent>> LoadAsync(
        WalletId walletId,
        long fromSequenceNoInclusive,
        CancellationToken ct = default);

    /// <summary>Append a single event to a wallet's stream.</summary>
    /// <param name="event">The event to append.</param>
    /// <param name="expectedCurrentVersion">The version the caller observed (sequence number of the most recently loaded event, or 0 for a brand-new stream).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The append outcome — either the appended event + new version, or the original event when an idempotency replay short-circuited the append.</returns>
    /// <exception cref="WalletConcurrencyConflictException">Thrown when another writer has advanced the stream.</exception>
    /// <exception cref="IdempotencyKeyMismatchException">Thrown when an idempotency key replay carries a different payload than the original.</exception>
    Task<AppendOutcome> AppendAsync(
        IWalletEvent @event,
        long expectedCurrentVersion,
        CancellationToken ct = default);
}

/// <summary>Outcome of an <see cref="IWalletEventStore.AppendAsync"/> call.</summary>
/// <param name="Event">The event present in the stream (the just-appended event, or the original on idempotency replay).</param>
/// <param name="ResultingVersion">The new stream version.</param>
/// <param name="WasIdempotencyReplay"><see langword="true"/> when the call short-circuited because the idempotency key was already recorded.</param>
public sealed record AppendOutcome(
    IWalletEvent Event,
    long ResultingVersion,
    bool WasIdempotencyReplay);
