using Microsoft.EntityFrameworkCore;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore;

/// <summary>
/// The EF Core <see cref="DbContext"/> that backs the SQL-based tenant registry.
/// </summary>
/// <remarks>
/// <para>
/// Holds the <see cref="PolarTenantInfoEntity"/> rows that PolarSharp resolves on every
/// request via Finbuckle's tenant store interface. Unlike catalog / identity / reporting
/// DbContexts, this context is NOT tenant-scoped: it IS the tenant registry, so it must be
/// readable across tenants by the bootstrap lookup. Application-level filtering happens at
/// the consuming layer (<c>EfMultiTenantStore</c>), not via global query filter.
/// </para>
/// <para>
/// Provider-specific subclasses (<c>SqlServerPolarTenantDbContext</c>, etc.) supply the
/// concrete connection string and migrations. The <see cref="OnModelCreating"/> method below
/// applies the core relational schema; providers may extend with provider-specific
/// configuration (e.g., RLS DDL is part of the migrations, not the model).
/// </para>
/// </remarks>
public class PolarTenantDbContext : DbContext
{
    /// <summary>Initializes a new <see cref="PolarTenantDbContext"/> with the supplied options.</summary>
    /// <param name="options">EF Core options including provider and connection string.</param>
    public PolarTenantDbContext(DbContextOptions<PolarTenantDbContext> options) : base(options) { }

    /// <summary>Gets the tenant registry table.</summary>
    public DbSet<PolarTenantInfoEntity> Tenants => Set<PolarTenantInfoEntity>();

    /// <summary>
    /// Gets the one-time upgrade history table — the persistent completion-marker store
    /// read by <see cref="ISingleTenantUpgradeMigrator.HasUpgradeCompletedAsync(CancellationToken)"/>
    /// implementations.
    /// </summary>
    /// <remarks>
    /// Lives alongside the tenant registry because both are platform-scoped, not tenant-scoped:
    /// they describe the deployment itself rather than any one tenant's data. On SQLite the
    /// table physically resides in <c>master_SaaS.db</c>; on the other providers it shares
    /// the same database as the <c>polar_tenants</c> table.
    /// </remarks>
    public DbSet<UpgradeHistoryEntity> UpgradeHistory => Set<UpgradeHistoryEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        // The EF Core Cosmos provider rejects HasIndex(...) calls outright — Cosmos
        // containers index every property by default, so explicit secondary indexes are
        // neither necessary nor supported. Detect non-relational providers (Cosmos is
        // the only non-relational provider PolarSharp ships) and skip the index
        // declarations so the same DbContext class can be reused across SqlServer,
        // PostgreSQL, MariaDB, SQLite (which all need the indexes) and Cosmos (which
        // rejects them). Using IsRelational() instead of IsCosmos() keeps this base
        // package free of the Microsoft.EntityFrameworkCore.Cosmos transitive dep —
        // SQL hosts shouldn't pull in the Cosmos SDK just to know they aren't Cosmos.
        var isRelational = Database.IsRelational();

        modelBuilder.Entity<PolarTenantInfoEntity>(e =>
        {
            e.ToTable("polar_tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64).IsRequired();
            if (isRelational)
            {
                e.HasIndex(x => x.Identifier).IsUnique();
            }
            e.Property(x => x.Identifier).HasMaxLength(128).IsRequired();
            // Inherited from PolarTenantBase:
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(128).IsRequired();
            e.Property(x => x.Country).HasMaxLength(2);
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Website).HasMaxLength(2048);
            e.Property(x => x.AvatarUrl).HasMaxLength(2048);
            e.Property(x => x.DefaultPresentmentCurrency).HasMaxLength(3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.AccountId).HasMaxLength(128);
            e.Property(x => x.PayoutAccountId).HasMaxLength(128);
            // PolarSharp-internal:
            e.Property(x => x.PolarAccessToken).HasMaxLength(512).IsRequired();
            e.Property(x => x.WebhookEndpointId).HasMaxLength(128);
            e.Property(x => x.WebhookSecret).HasMaxLength(256);
            e.Property(x => x.Server).HasConversion<string>().HasMaxLength(32);
            // v1.3.x lifecycle columns:
            e.Property(x => x.LifecycleStatus).HasConversion<int>();
            e.Property(x => x.SiteManagerEmail).HasMaxLength(320).IsRequired();
            e.Property(x => x.SiteManagerEmailVerified);
            e.Property(x => x.SiteManagerPhone).HasMaxLength(32);
            e.Ignore(x => x.TenantId);   // computed property — not a column
        });

        modelBuilder.Entity<UpgradeHistoryEntity>(e =>
        {
            e.ToTable("polar_upgrade_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.UpgradeKind).HasMaxLength(64).IsRequired();
            e.Property(x => x.CompletedAt).IsRequired();
            e.Property(x => x.ActorUserId).HasMaxLength(64);
            e.Property(x => x.Message);
            e.Property(x => x.ResultSummaryJson);
            if (isRelational)
            {
                e.HasIndex(x => x.UpgradeKind);
            }
        });
    }
}
