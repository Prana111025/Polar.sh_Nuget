using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Entities;
using PolarSharp.Reporting.EntityFrameworkCore.MariaDb;
using Testcontainers.MariaDb;

namespace PolarSharp.Reporting.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the MariaDB reporting provider variant
/// (<see cref="MariaDbReportingExtensions.UseMariaDbReporting"/>) against a real MariaDB
/// 11.5 container spun up by Testcontainers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why an integration test.</strong> The MariaDB reporting wiring drives a real
/// EF Core migration pipeline against the Oracle <c>MySql.EntityFrameworkCore</c> provider,
/// includes the <c>MariaDbCompatibleHistoryRepository</c> swap that fixes the GET_LOCK
/// NULL-cast incompatibility, and round-trips real <c>DateTime</c> + <c>bigint</c> columns
/// without the SQLite value-converter detour. None of those code paths execute against the
/// in-memory SQLite test harness.
/// </para>
/// <para>
/// <strong>Why MariaDB has no RLS layer.</strong> MariaDB / MySQL do not expose
/// Postgres-style <c>ROW LEVEL SECURITY</c> nor SQL Server's
/// <c>SESSION_CONTEXT</c>-driven security policies, so per-tenant isolation on the
/// reporting tables is enforced solely by the EF Core global query filter inherited from
/// <see cref="TenantAwareDbContextBase"/>. The "Per_tenant_query_filter" test below covers
/// the only isolation layer this provider has.
/// </para>
/// <para>
/// <strong>CI category filtering convention</strong>:
/// </para>
/// <list type="bullet">
///   <item><description><c>dotnet test --filter "Category!=Integration"</c> — fast unit run.</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration"</c> — all integration tests.</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration&amp;Provider=MariaDb"</c> — this provider only.</description></item>
/// </list>
/// <para>
/// <strong>Container lifecycle.</strong> One container per test class via
/// <see cref="IAsyncLifetime"/>. MariaDB container startup is ~10–20s; the full class runs
/// in ~25–45s. Tests are order-independent — every test that asserts on snapshot rows
/// clears its own table at the start.
/// </para>
/// <para>
/// <strong>Image pin.</strong> <c>mariadb:11.5</c> is pinned deliberately so the container
/// shape stays reproducible across machines (no implicit "latest" drift).
/// </para>
/// <para>
/// <strong>Prerequisite.</strong> Docker must be running on the host.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "MariaDb")]
public sealed class MariaDbReportingDbIntegrationTests : IAsyncLifetime
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    private MariaDbContainer _container = null!;
    private MutableTenantAccessor _accessor = null!;
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        // Pin the image so the container shape is reproducible across machines. The
        // image-name constructor overload is required — Testcontainers 4.11 deprecated the
        // parameterless MariaDbBuilder().
        _container = new MariaDbBuilder("mariadb:11.5")
            .Build();
        await _container.StartAsync();

        _accessor = new MutableTenantAccessor(TenantA);

        // Wire the production extension method end-to-end. UseMariaDbReporting does NOT
        // register an interceptor (MariaDB has no DB-layer RLS to feed session state into),
        // but the DbContext still needs IMultiTenantContextAccessor (and IFakeDataPolicy /
        // IAppMasterAdminCrossTenantContext are optional with safe fallbacks) so the
        // global query filter resolves a current tenant on construction.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMultiTenantContextAccessor>(_accessor);
        services.AddSingleton<IAppMasterAdminCrossTenantContext>(new NoCrossTenantContext());
        services.UseMariaDbReporting(_container.GetConnectionString());

        _services = services.BuildServiceProvider();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    // --- Migrations -------------------------------------------------------------------

    /// <summary>
    /// Applying the MariaDB reporting migrations against the live container must complete
    /// without error. The very fact that <see cref="DatabaseFacade.MigrateAsync"/>
    /// succeeds is also the proof that the <c>MariaDbCompatibleHistoryRepository</c>
    /// production swap works — without it, the Oracle provider's <c>GET_LOCK</c> call
    /// would NULL-cast-throw on MariaDB.
    /// </summary>
    [Fact]
    public async Task Container_boots_and_migrations_apply_cleanly()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();

        await db.Database.MigrateAsync();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, m => m.EndsWith("_Initial", StringComparison.Ordinal));
    }

    /// <summary>
    /// Re-running <see cref="DatabaseFacade.MigrateAsync"/> against an already-migrated
    /// database must succeed and produce no additional applied-migration rows. Confirms
    /// the migration pipeline is idempotent on the MariaDB provider.
    /// </summary>
    [Fact]
    public async Task Migrations_are_idempotent_on_re_run()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();

        await db.Database.MigrateAsync();
        var afterFirst = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        await db.Database.MigrateAsync();
        var afterSecond = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        Assert.Equal(afterFirst.Count, afterSecond.Count);
        Assert.Equal(afterFirst.OrderBy(x => x), afterSecond.OrderBy(x => x));
    }

    // --- Snapshot round-trip ----------------------------------------------------------

    /// <summary>
    /// Inserting a <see cref="ReportBenefitEntity"/> through the real MariaDB provider and
    /// re-reading it via a fresh scope must surface every field byte-identical. Covers
    /// the MariaDB native <c>DATETIME</c> + <c>TINYINT</c> + <c>VARCHAR</c> mappings
    /// without any SQLite value-converter detour.
    /// </summary>
    [Fact]
    public async Task Snapshot_row_round_trips_via_real_provider()
    {
        await EnsureMigratedAndClearBenefitsAsync();
        _accessor.SwitchTo(TenantA);

        var benefitId = Guid.NewGuid();
        // MariaDB DATETIME stores second-precision by default; build the comparison values
        // to second precision so the equality assertion is provider-agnostic.
        var createdAt = new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
        var modifiedAt = createdAt.AddDays(3);

        using (var writeScope = _services.CreateScope())
        {
            var db = writeScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            db.Benefits.Add(new ReportBenefitEntity
            {
                Id = benefitId,
                TenantId = TenantA,
                PolarBenefitId = "bnf_round_trip_mariadb",
                Name = "Round Trip Benefit",
                Kind = "license_keys",
                Description = "Issued for the round-trip integration test.",
                IsActive = true,
                CreatedAt = createdAt,
                ModifiedAt = modifiedAt,
                IsFakeData = false,
            });
            await db.SaveChangesAsync();
        }

        using var readScope = _services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var roundTripped = await readDb.Benefits.AsNoTracking().SingleAsync(b => b.Id == benefitId);

        Assert.Equal(TenantA, roundTripped.TenantId);
        Assert.Equal("bnf_round_trip_mariadb", roundTripped.PolarBenefitId);
        Assert.Equal("Round Trip Benefit", roundTripped.Name);
        Assert.Equal("license_keys", roundTripped.Kind);
        Assert.Equal("Issued for the round-trip integration test.", roundTripped.Description);
        Assert.True(roundTripped.IsActive);
        Assert.Equal(createdAt, roundTripped.CreatedAt);
        Assert.Equal(modifiedAt, roundTripped.ModifiedAt);
        Assert.False(roundTripped.IsFakeData);
    }

    // --- Per-tenant query filter ------------------------------------------------------

    /// <summary>
    /// With only the EF Core global query filter to enforce tenant isolation (MariaDB
    /// lacks RLS), inserting benefits for Tenant A and Tenant B and then querying while
    /// the current tenant is A must only surface A's rows. Confirms the global query
    /// filter applies correctly against the MariaDB provider — the sole isolation layer.
    /// </summary>
    [Fact]
    public async Task Per_tenant_query_filter_scopes_results_to_current_tenant()
    {
        await EnsureMigratedAndClearBenefitsAsync();

        var aBenefitId = Guid.NewGuid();
        var bBenefitId = Guid.NewGuid();

        _accessor.SwitchTo(TenantA);
        using (var aScope = _services.CreateScope())
        {
            var db = aScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            db.Benefits.Add(NewBenefit(aBenefitId, TenantA, "bnf_tenant_a"));
            await db.SaveChangesAsync();
        }

        _accessor.SwitchTo(TenantB);
        using (var bScope = _services.CreateScope())
        {
            var db = bScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            db.Benefits.Add(NewBenefit(bBenefitId, TenantB, "bnf_tenant_b"));
            await db.SaveChangesAsync();
        }

        _accessor.SwitchTo(TenantA);
        using var readScope = _services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var visible = await readDb.Benefits.AsNoTracking().ToListAsync();

        Assert.Single(visible);
        Assert.Equal(aBenefitId, visible[0].Id);
        Assert.Equal(TenantA, visible[0].TenantId);
        Assert.DoesNotContain(visible, b => b.Id == bBenefitId);
    }

    // --- helpers ----------------------------------------------------------------------

    /// <summary>
    /// Ensures migrations are applied and clears <c>polar_report_benefits</c> so the
    /// calling test owns the table's state. xUnit does not guarantee order within a class,
    /// so every test establishes its own clean baseline. No RLS on MariaDB means a single
    /// raw DELETE removes every row regardless of the current accessor tenant.
    /// </summary>
    private async Task EnsureMigratedAndClearBenefitsAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM polar_report_benefits;");
    }

    private static ReportBenefitEntity NewBenefit(Guid id, string tenantId, string polarBenefitId) => new()
    {
        Id = id,
        TenantId = tenantId,
        PolarBenefitId = polarBenefitId,
        Name = $"Benefit for {tenantId}",
        Kind = "license_keys",
        Description = null,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        ModifiedAt = null,
        IsFakeData = false,
    };

    /// <summary>Mutable accessor so a single test can swap tenants between operations.</summary>
    private sealed class MutableTenantAccessor : IMultiTenantContextAccessor
    {
        private IMultiTenantContext _current;

        public MutableTenantAccessor(string tenantId) => _current = Build(tenantId);

        public IMultiTenantContext MultiTenantContext
        {
            get => _current;
            set => _current = value;
        }

        public void SwitchTo(string tenantId) => _current = Build(tenantId);

        private static IMultiTenantContext Build(string tenantId) =>
            new MultiTenantContext<PolarTenantInfo>(
                new PolarTenantInfo { Id = tenantId, Identifier = tenantId, Name = tenantId });
    }

    /// <summary>Test stub: never grants cross-tenant access — matches the production default
    /// when PolarSharp.MultiTenant.Identity isn't installed.</summary>
    private sealed class NoCrossTenantContext : IAppMasterAdminCrossTenantContext
    {
        public bool IsAllowedCrossTenantAccess => false;
    }
}
