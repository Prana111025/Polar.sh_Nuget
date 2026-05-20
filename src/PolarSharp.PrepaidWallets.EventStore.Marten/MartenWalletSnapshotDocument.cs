using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

namespace PolarSharp.PrepaidWallets.EventStore.Marten;

/// <summary>
/// Marten document mirror of <see cref="WalletSnapshot"/>. Marten requires a <c>Guid Id</c>
/// property for document storage; the wallet's <see cref="WalletId"/> wrapper isn't directly usable
/// as Marten's identifier, so this document type holds the raw <see cref="Guid"/> and converts.
/// </summary>
public sealed class MartenWalletSnapshotDocument
{
    /// <summary>The wallet identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Aggregate version at snapshot time.</summary>
    public long Version { get; set; }

    /// <summary>Customer who owns the wallet.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Tenant scope (nullable for single-tenant deployments).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>ISO-4217 currency code.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Token balance.</summary>
    public long BalanceTokens { get; set; }

    /// <summary>Lifecycle status.</summary>
    public WalletStatus Status { get; set; }

    /// <summary>UTC timestamp of WalletOpened.</summary>
    public DateTimeOffset OpenedAt { get; set; }

    /// <summary>UTC timestamp of the most recent event at snapshot time.</summary>
    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>UTC timestamp the snapshot was taken.</summary>
    public DateTimeOffset TakenAt { get; set; }

    /// <summary>Per-bucket FIFO state at snapshot time. Marten serializes this list with the rest of the document.</summary>
    public IReadOnlyList<FundingBucketState> RemainingBuckets { get; set; } = Array.Empty<FundingBucketState>();

    /// <summary>Convert from a domain <see cref="WalletSnapshot"/>.</summary>
    /// <param name="snapshot">The domain snapshot.</param>
    public static MartenWalletSnapshotDocument FromSnapshot(WalletSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new MartenWalletSnapshotDocument
        {
            Id = snapshot.WalletId.Value,
            Version = snapshot.Version,
            CustomerId = snapshot.CustomerId,
            TenantId = snapshot.TenantId.TryGetValue(out var t) ? t : null,
            Currency = snapshot.Currency,
            BalanceTokens = snapshot.Balance.Value,
            Status = snapshot.Status,
            OpenedAt = snapshot.OpenedAt,
            LastActivityAt = snapshot.LastActivityAt,
            TakenAt = snapshot.TakenAt,
            RemainingBuckets = snapshot.RemainingBuckets,
        };
    }

    /// <summary>Convert to a domain <see cref="WalletSnapshot"/>.</summary>
    public WalletSnapshot ToSnapshot() =>
        new(
            new WalletId(Id),
            Version,
            CustomerId,
            TenantId.HasValue ? Option<Guid>.Some(TenantId.Value) : Option<Guid>.None,
            Currency,
            new TokenAmount(BalanceTokens),
            Status,
            OpenedAt,
            LastActivityAt,
            TakenAt,
            RemainingBuckets);
}
