namespace PolarSharp.PrepaidWallets.Abstractions.Stores;

/// <summary>
/// Persistence boundary for wallet snapshots. Snapshots compress the cost of replaying long event
/// streams: the wallet aggregate is loaded from the latest snapshot + the events appended since.
/// </summary>
/// <remarks>
/// Per Case Study 02 step 5 ("Snapshot strategy"), the default is one snapshot every 50 events;
/// Cosmos-backed stores tighten that to every 10 events because full replay is RU-expensive.
/// </remarks>
public interface IWalletSnapshotStore
{
    /// <summary>Load the latest snapshot for a wallet, if one exists.</summary>
    /// <param name="walletId">The wallet identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The latest snapshot, or <see cref="Option{T}.None"/> when none has been written yet.</returns>
    Task<Option<WalletSnapshot>> LoadLatestAsync(WalletId walletId, CancellationToken ct = default);

    /// <summary>Persist a snapshot.</summary>
    /// <param name="snapshot">The snapshot to save.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(WalletSnapshot snapshot, CancellationToken ct = default);
}

/// <summary>Denormalized snapshot of a wallet's state at a specific sequence number.</summary>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="Version">Sequence number of the last event applied into this snapshot.</param>
/// <param name="CustomerId">Customer who owns the wallet.</param>
/// <param name="TenantId">Tenant scope.</param>
/// <param name="Currency">ISO-4217 currency code.</param>
/// <param name="Balance">Token balance at <paramref name="Version"/>.</param>
/// <param name="Status">Lifecycle status at <paramref name="Version"/>.</param>
/// <param name="OpenedAt">UTC timestamp of the <c>WalletOpened</c> event.</param>
/// <param name="LastActivityAt">UTC timestamp of the most recent event at snapshot time.</param>
/// <param name="TakenAt">UTC timestamp the snapshot was taken.</param>
/// <param name="RemainingBuckets">
/// Per-bucket FIFO state at snapshot time. The list is in funding-event sequence order (oldest
/// first) so a snapshot+delta load resumes spending oldest tokens first without re-reading the
/// full event stream. Zero-remaining buckets are omitted.
/// </param>
public sealed record WalletSnapshot(
    WalletId WalletId,
    long Version,
    Guid CustomerId,
    Option<Guid> TenantId,
    string Currency,
    TokenAmount Balance,
    WalletStatus Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset LastActivityAt,
    DateTimeOffset TakenAt,
    IReadOnlyList<FundingBucketState> RemainingBuckets);
