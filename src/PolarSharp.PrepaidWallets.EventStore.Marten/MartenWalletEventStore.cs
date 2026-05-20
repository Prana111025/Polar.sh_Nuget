using Marten;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

namespace PolarSharp.PrepaidWallets.EventStore.Marten;

/// <summary>
/// Marten-backed implementation of <see cref="IWalletEventStore"/>. Each wallet maps to a Marten
/// event stream keyed by the wallet id. Optimistic concurrency rides on Marten's
/// <c>expectedVersion</c> argument; idempotency is enforced by reading the stream before
/// appending.
/// </summary>
/// <remarks>
/// <para>
/// Per Case Study 02 step 1 ("Choose the event-store backend"), Marten is the natural choice when
/// the host's database is Postgres — native event-sourcing primitives, no impedance mismatch with
/// the aggregate model.
/// </para>
/// <para>
/// Hosts must register each wallet event type with Marten's <c>StoreOptions.Events.AddEventType</c>
/// at configuration time (handled by <see cref="MartenWalletEventStoreExtensions.UseMartenWalletEventStore"/>).
/// </para>
/// </remarks>
public sealed class MartenWalletEventStore : IWalletEventStore
{
    private readonly IDocumentStore _store;

    /// <summary>Construct the store.</summary>
    /// <param name="store">The Marten document store.</param>
    public MartenWalletEventStore(IDocumentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IWalletEvent>> LoadAsync(
        WalletId walletId,
        long fromSequenceNoInclusive,
        CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var stream = await session.Events.FetchStreamAsync(walletId.Value, token: ct).ConfigureAwait(false);
        var result = new List<IWalletEvent>(stream.Count);
        foreach (var entry in stream)
        {
            if (entry.Data is IWalletEvent walletEvent && walletEvent.SequenceNo >= fromSequenceNoInclusive)
            {
                result.Add(walletEvent);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<AppendOutcome> AppendAsync(
        IWalletEvent @event,
        long expectedCurrentVersion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        await using var session = _store.LightweightSession();
        var existing = await session.Events.FetchStreamAsync(@event.WalletId.Value, token: ct).ConfigureAwait(false);

        foreach (var entry in existing)
        {
            if (entry.Data is IWalletEvent priorEvent
                && priorEvent.IdempotencyKey == @event.IdempotencyKey)
            {
                return new AppendOutcome(priorEvent, ResultingVersion: existing.Count, WasIdempotencyReplay: true);
            }
        }

        var observedVersion = existing.Count;
        if (observedVersion != expectedCurrentVersion)
        {
            throw new WalletConcurrencyConflictException(
                @event.WalletId,
                expectedCurrentVersion,
                observedVersion);
        }

        if (observedVersion == 0)
        {
            session.Events.StartStream(@event.WalletId.Value, @event);
        }
        else
        {
            session.Events.Append(@event.WalletId.Value, observedVersion + 1, @event);
        }

        // Concurrency races between the FetchStreamAsync above and SaveChangesAsync below surface
        // as a MartenException; the wallet-core's MediatR retry behavior translates the resulting
        // failure and re-attempts the command.
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        return new AppendOutcome(@event, ResultingVersion: observedVersion + 1, WasIdempotencyReplay: false);
    }
}
