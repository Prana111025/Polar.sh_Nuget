using Marten;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

namespace PolarSharp.PrepaidWallets.EventStore.Marten;

/// <summary>
/// Marten-backed wallet snapshot store. Snapshots are stored as a Marten document keyed by
/// wallet id; later writes overwrite earlier snapshots only when they advance the version.
/// </summary>
public sealed class MartenWalletSnapshotStore : IWalletSnapshotStore
{
    private readonly IDocumentStore _store;

    /// <summary>Construct the snapshot store.</summary>
    /// <param name="store">The Marten document store.</param>
    public MartenWalletSnapshotStore(IDocumentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<Option<WalletSnapshot>> LoadLatestAsync(WalletId walletId, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var snapshot = await session.LoadAsync<MartenWalletSnapshotDocument>(walletId.Value, ct).ConfigureAwait(false);
        return snapshot is null ? Option<WalletSnapshot>.None : Option<WalletSnapshot>.Some(snapshot.ToSnapshot());
    }

    /// <inheritdoc/>
    public async Task SaveAsync(WalletSnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<MartenWalletSnapshotDocument>(snapshot.WalletId.Value, ct).ConfigureAwait(false);
        if (existing is not null && existing.Version >= snapshot.Version)
        {
            return;
        }

        session.Store(MartenWalletSnapshotDocument.FromSnapshot(snapshot));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
