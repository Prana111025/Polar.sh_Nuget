namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore;

/// <summary>
/// EF Core entity mapped to the <c>wallet_snapshots</c> table — one row per (wallet, snapshot
/// version) pair, with the denormalized snapshot state for fast aggregate load.
/// </summary>
public sealed class WalletSnapshotRecord
{
    /// <summary>Foreign key into <c>wallet_events.wallet_id</c>. Composite key with <see cref="Version"/>.</summary>
    public Guid WalletId { get; set; }

    /// <summary>Stream sequence number this snapshot covers up to.</summary>
    public long Version { get; set; }

    /// <summary>Customer who owns the wallet.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Tenant scope (nullable for single-tenant deployments).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>ISO-4217 currency code.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Token balance at <see cref="Version"/>.</summary>
    public long BalanceTokens { get; set; }

    /// <summary>Lifecycle status at <see cref="Version"/>.</summary>
    public int StatusCode { get; set; }

    /// <summary>UTC timestamp of WalletOpened.</summary>
    public DateTimeOffset OpenedAt { get; set; }

    /// <summary>UTC timestamp of the most recent event at snapshot time.</summary>
    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>UTC timestamp the snapshot was taken.</summary>
    public DateTimeOffset TakenAt { get; set; }
}
