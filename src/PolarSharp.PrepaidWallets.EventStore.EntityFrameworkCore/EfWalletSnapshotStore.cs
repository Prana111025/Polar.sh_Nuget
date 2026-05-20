using Microsoft.EntityFrameworkCore;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IWalletSnapshotStore"/>. Snapshots are stored in
/// <see cref="WalletEventStoreDbContext.WalletSnapshots"/> with a composite key on
/// <c>(wallet_id, version)</c>; only the most recent snapshot is read at load time.
/// </summary>
public sealed class EfWalletSnapshotStore : IWalletSnapshotStore
{
    private readonly WalletEventStoreDbContext _db;

    /// <summary>Construct the snapshot store.</summary>
    /// <param name="db">The EF Core context.</param>
    public EfWalletSnapshotStore(WalletEventStoreDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<Option<WalletSnapshot>> LoadLatestAsync(WalletId walletId, CancellationToken ct = default)
    {
        var row = await _db.WalletSnapshots
            .AsNoTracking()
            .Where(x => x.WalletId == walletId.Value)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Option<WalletSnapshot>.None;
        }

        return Option<WalletSnapshot>.Some(FromRow(row));
    }

    /// <inheritdoc/>
    public async Task SaveAsync(WalletSnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var existing = await _db.WalletSnapshots
            .FirstOrDefaultAsync(
                x => x.WalletId == snapshot.WalletId.Value && x.Version == snapshot.Version,
                ct)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return;
        }

        _db.WalletSnapshots.Add(ToRow(snapshot));
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static WalletSnapshotRecord ToRow(WalletSnapshot s) =>
        new()
        {
            WalletId = s.WalletId.Value,
            Version = s.Version,
            CustomerId = s.CustomerId,
            TenantId = s.TenantId.TryGetValue(out var t) ? t : null,
            Currency = s.Currency,
            BalanceTokens = s.Balance.Value,
            StatusCode = (int)s.Status,
            OpenedAt = s.OpenedAt,
            LastActivityAt = s.LastActivityAt,
            TakenAt = s.TakenAt,
        };

    private static WalletSnapshot FromRow(WalletSnapshotRecord r) =>
        new(
            new WalletId(r.WalletId),
            r.Version,
            r.CustomerId,
            r.TenantId.HasValue ? Option<Guid>.Some(r.TenantId.Value) : Option<Guid>.None,
            r.Currency,
            new TokenAmount(r.BalanceTokens),
            (WalletStatus)r.StatusCode,
            r.OpenedAt,
            r.LastActivityAt,
            r.TakenAt);
}
