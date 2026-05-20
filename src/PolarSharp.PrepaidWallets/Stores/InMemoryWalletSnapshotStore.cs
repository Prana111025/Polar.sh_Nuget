using System.Collections.Concurrent;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

namespace PolarSharp.PrepaidWallets.Stores;

/// <summary>In-memory snapshot store. Mirror of <see cref="InMemoryWalletEventStore"/> for testing.</summary>
public sealed class InMemoryWalletSnapshotStore : IWalletSnapshotStore
{
    private readonly ConcurrentDictionary<WalletId, WalletSnapshot> _latest = new();

    /// <inheritdoc/>
    public Task<Option<WalletSnapshot>> LoadLatestAsync(WalletId walletId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(
            _latest.TryGetValue(walletId, out var snap)
                ? Option<WalletSnapshot>.Some(snap)
                : Option<WalletSnapshot>.None);
    }

    /// <inheritdoc/>
    public Task SaveAsync(WalletSnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ct.ThrowIfCancellationRequested();

        _latest.AddOrUpdate(
            snapshot.WalletId,
            snapshot,
            (_, existing) => snapshot.Version > existing.Version ? snapshot : existing);
        return Task.CompletedTask;
    }

    /// <summary>Diagnostic — number of wallets that have written at least one snapshot.</summary>
    public int SnapshotCount => _latest.Count;
}
