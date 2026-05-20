using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.Domain;

namespace PolarSharp.PrepaidWallets.Handlers;

/// <summary>
/// Loads a <see cref="Wallet"/> aggregate by id — preferring a snapshot + delta-replay path when
/// a snapshot is available, falling back to a full event-stream replay when not.
/// </summary>
/// <remarks>
/// Per Case Study 02 step 5 ("Snapshot strategy"), loading from a snapshot + replaying the
/// events appended since the snapshot was taken bounds load latency on long event streams.
/// </remarks>
public sealed class WalletAggregateLoader
{
    private readonly IWalletEventStore _events;
    private readonly IWalletSnapshotStore _snapshots;

    /// <summary>Construct the loader.</summary>
    /// <param name="events">The event store.</param>
    /// <param name="snapshots">The snapshot store.</param>
    public WalletAggregateLoader(IWalletEventStore events, IWalletSnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(snapshots);
        _events = events;
        _snapshots = snapshots;
    }

    /// <summary>
    /// Load a wallet by id. Returns <see cref="Option{T}.None"/> when no events for the wallet exist
    /// (i.e. the wallet has not been opened yet).
    /// </summary>
    /// <param name="walletId">The wallet identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Option<Wallet>> LoadAsync(WalletId walletId, CancellationToken ct = default)
    {
        var snapshotResult = await _snapshots.LoadLatestAsync(walletId, ct).ConfigureAwait(false);

        if (snapshotResult.TryGetValue(out var snapshot))
        {
            var delta = await _events.LoadAsync(walletId, snapshot.Version + 1, ct).ConfigureAwait(false);
            return Option<Wallet>.Some(Wallet.RehydrateFromSnapshot(snapshot, delta));
        }

        var events = await _events.LoadAsync(walletId, fromSequenceNoInclusive: 1, ct).ConfigureAwait(false);
        if (events.Count == 0)
        {
            return Option<Wallet>.None;
        }

        return Option<Wallet>.Some(Wallet.Rehydrate(events));
    }
}
