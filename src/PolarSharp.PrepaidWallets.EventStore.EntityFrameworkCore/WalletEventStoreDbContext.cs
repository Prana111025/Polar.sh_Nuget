using Microsoft.EntityFrameworkCore;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore;

/// <summary>
/// Abstract EF Core DbContext for the wallet event store. Provider packages (SQL Server, SQLite,
/// PostgreSQL, MariaDB, Cosmos) subclass this base and apply provider-specific configuration in
/// <see cref="DbContext.OnConfiguring(DbContextOptionsBuilder)"/>.
/// </summary>
/// <remarks>
/// <para>
/// The schema is defined declaratively in <see cref="OnModelCreating(ModelBuilder)"/>: two tables
/// (<c>wallet_events</c> and <c>wallet_snapshots</c>), one unique index per stream pair
/// (<c>(wallet_id, sequence_no)</c>), and one unique index per idempotency key
/// (<c>(wallet_id, idempotency_key)</c>) that powers idempotency enforcement.
/// </para>
/// </remarks>
public abstract class WalletEventStoreDbContext : DbContext
{
    /// <summary>Construct the context with the standard options.</summary>
    /// <param name="options">EF Core options.</param>
    protected WalletEventStoreDbContext(DbContextOptions options)
        : base(options) { }

    /// <summary>The <c>wallet_events</c> table.</summary>
    public DbSet<WalletEventRecord> WalletEvents => Set<WalletEventRecord>();

    /// <summary>The <c>wallet_snapshots</c> table.</summary>
    public DbSet<WalletSnapshotRecord> WalletSnapshots => Set<WalletSnapshotRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var events = modelBuilder.Entity<WalletEventRecord>();
        events.ToTable("wallet_events");
        events.HasKey(x => x.Id);
        events.Property(x => x.Id).ValueGeneratedNever();
        events.Property(x => x.WalletId).IsRequired();
        events.Property(x => x.SequenceNo).IsRequired();
        events.Property(x => x.EventType).IsRequired().HasMaxLength(64);
        events.Property(x => x.EventPayloadJson).IsRequired();
        events.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(IdempotencyKeyMaxLength);
        events.Property(x => x.OccurredAt).IsRequired();
        events.Property(x => x.ActorUserId).IsRequired();
        events.Property(x => x.TenantId).IsRequired(false);
        events.HasIndex(x => new { x.WalletId, x.SequenceNo }).IsUnique();
        events.HasIndex(x => new { x.WalletId, x.IdempotencyKey }).IsUnique();
        // Index supports Phase 22.5 WTR tax-aggregation queries spanning all wallets of a tenant
        // across a date range. The leading column is TenantId; OccurredAt is descending so the
        // most-recent events appear first in normal "what happened lately" queries too.
        events.HasIndex(x => new { x.TenantId, x.OccurredAt }).HasDatabaseName("ix_wallet_events_tenant_id_occurred_at");

        var snapshots = modelBuilder.Entity<WalletSnapshotRecord>();
        snapshots.ToTable("wallet_snapshots");
        snapshots.HasKey(x => new { x.WalletId, x.Version });
        snapshots.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        snapshots.Property(x => x.StatusCode).IsRequired();
        snapshots.Property(x => x.BalanceTokens).IsRequired();
        snapshots.Property(x => x.BucketsJson).IsRequired();
    }

    /// <summary>Maximum stored idempotency-key length; matches the abstraction's constant.</summary>
    public const int IdempotencyKeyMaxLength = 128;
}
