using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Entities;
using PolarSharp.Reporting.EntityFrameworkCore.SqlServer;
using Testcontainers.MsSql;

namespace PolarSharp.Reporting.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the SQL Server reporting provider variant
/// (<see cref="SqlServerReportingExtensions.UseSqlServerReporting"/>) against a real
/// SQL Server 2022 container spun up by Testcontainers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why an integration test.</strong> The SQL Server reporting wiring drives a real
/// EF Core migration pipeline (Initial + ModelDriftCatchup + EnableRowLevelSecurity), a
/// production-shape <see cref="SqlServerTenantSessionInterceptor"/> that issues
/// <c>sys.sp_set_session_context</c> on every connection open, and a real RLS security
/// policy that filters every <c>ITenantOwned</c> snapshot table by SESSION_CONTEXT-derived
/// tenant id. None of those code paths execute against the in-memory SQLite test harness
/// (<c>ReportingTestContext</c>), which lacks SESSION_CONTEXT and RLS. This class proves
/// the reporting DbContext round-trips data correctly against the engine it ships for.
/// </para>
/// <para>
/// <strong>CI category filtering convention</strong> (established by the MultiTenant
/// Phase 2a integration tests):
/// </para>
/// <list type="bullet">
///   <item><description><c>dotnet test</c> — runs <em>all</em> tests including integration tests. Slow.</description></item>
///   <item><description><c>dotnet test --filter "Category!=Integration"</c> — unit tests only. Fast (~seconds).</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration"</c> — integration tests only.</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration&amp;Provider=SqlServer"</c> — this provider only.</description></item>
/// </list>
/// <para>
/// <strong>Container lifecycle.</strong> One container per test class via
/// <see cref="IAsyncLifetime"/>. SQL Server container startup is ~10–20s; the full class
/// runs in ~30–60s. Tests must be order-independent because xUnit does not guarantee
/// execution order within a class — every test that asserts on snapshot rows clears its
/// own table at the start of the test.
/// </para>
/// <para>
/// <strong>Image pin.</strong> <c>mcr.microsoft.com/mssql/server:2022-latest</c> is pinned
/// deliberately so the container shape stays reproducible across machines.
/// </para>
/// <para>
/// <strong>Prerequisite.</strong> Docker must be running on the host. The image pulls
/// automatically on first run.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "SqlServer")]
public sealed class SqlServerReportingDbIntegrationTests : IAsyncLifetime
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    private MsSqlContainer _container = null!;
    private MutableTenantAccessor _accessor = null!;
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        // Strong explicit password — Testcontainers' default is already strong, but pinning
        // an explicit value makes the connection string deterministic for ad-hoc debugging
        // against the running container during failure investigation. The image tag is also
        // pinned so the container shape is reproducible across machines (no implicit "latest"
        // drift from one test run to the next).
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("PolarSharp_Reporting_Integration_1!")
            .Build();
        await _container.StartAsync();

        _accessor = new MutableTenantAccessor(TenantA);

        // Wire the production extension method end-to-end. UseSqlServerReporting registers
        // the SqlServerTenantSessionInterceptor — which depends on IMultiTenantContextAccessor
        // + IAppMasterAdminCrossTenantContext — so the test harness must register stubs for
        // both before BuildServiceProvider so the interceptor can resolve its dependencies on
        // every connection open. The interceptor is what makes RLS happy: it sets
        // SESSION_CONTEXT('tenant_id') on every check-out, and the
        // EnableRowLevelSecurity migration's SECURITY POLICY filters every snapshot table by
        // that session value.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMultiTenantContextAccessor>(_accessor);
        services.AddSingleton<IAppMasterAdminCrossTenantContext>(new NoCrossTenantContext());
        services.UseSqlServerReporting(_container.GetConnectionString());

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
    /// Applying the SQL Server reporting migrations against the live container must
    /// complete without error. Covers <c>Initial</c>, <c>ModelDriftCatchup</c>, and
    /// <c>EnableRowLevelSecurity</c> migrations end-to-end.
    /// </summary>
    [Fact]
    public async Task Container_boots_and_migrations_apply_cleanly()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();

        await db.Database.MigrateAsync();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, m => m.EndsWith("_Initial", StringComparison.Ordinal));
        Assert.Contains(applied, m => m.EndsWith("_EnableRowLevelSecurity", StringComparison.Ordinal));
    }

    /// <summary>
    /// Re-running <see cref="DatabaseFacade.MigrateAsync"/> against an already-migrated
    /// database must succeed and produce no additional applied-migration rows. Confirms
    /// the migration pipeline is idempotent on the SQL Server provider.
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
    /// Inserting a <see cref="ReportOrderEntity"/> through the real SQL Server provider
    /// and re-reading it via a fresh scope must surface every field (including
    /// <see cref="DateTimeOffset"/> + <see cref="long"/> minor-unit columns) byte-identical
    /// to what was written. Covers the provider's native datetimeoffset + bigint mapping
    /// without the SQLite value-converter detour.
    /// </summary>
    [Fact]
    public async Task Snapshot_row_round_trips_via_real_provider()
    {
        await EnsureMigratedAndClearOrdersAsync();
        _accessor.SwitchTo(TenantA);

        var orderId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 5, 19, 10, 30, 0, TimeSpan.Zero);
        var fulfilledAt = createdAt.AddHours(2);

        using (var writeScope = _services.CreateScope())
        {
            var db = writeScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            db.Orders.Add(new ReportOrderEntity
            {
                Id = orderId,
                TenantId = TenantA,
                PolarOrderId = "ord_round_trip_sqlserver",
                OrderNumber = "INV-1001",
                CustomerId = "cus_round_trip",
                Status = "paid",
                Amount = 12345L,
                TaxAmount = 999L,
                RefundedAmount = 0L,
                Currency = "USD",
                LineItemCount = 3,
                InvoiceUrl = "https://example.test/invoices/1001.pdf",
                CreatedAt = createdAt,
                FulfilledAt = fulfilledAt,
                IsFakeData = false,
            });
            await db.SaveChangesAsync();
        }

        using var readScope = _services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var roundTripped = await readDb.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);

        Assert.Equal(TenantA, roundTripped.TenantId);
        Assert.Equal("ord_round_trip_sqlserver", roundTripped.PolarOrderId);
        Assert.Equal("INV-1001", roundTripped.OrderNumber);
        Assert.Equal("cus_round_trip", roundTripped.CustomerId);
        Assert.Equal("paid", roundTripped.Status);
        Assert.Equal(12345L, roundTripped.Amount);
        Assert.Equal(999L, roundTripped.TaxAmount);
        Assert.Equal(0L, roundTripped.RefundedAmount);
        Assert.Equal("USD", roundTripped.Currency);
        Assert.Equal(3, roundTripped.LineItemCount);
        Assert.Equal("https://example.test/invoices/1001.pdf", roundTripped.InvoiceUrl);
        Assert.Equal(createdAt, roundTripped.CreatedAt);
        Assert.Equal(fulfilledAt, roundTripped.FulfilledAt);
        Assert.False(roundTripped.IsFakeData);
    }

    // --- Per-tenant query filter ------------------------------------------------------

    /// <summary>
    /// With the EF Core global query filter AND the SQL Server RLS policy both active,
    /// inserting orders for Tenant A and Tenant B and then querying while the current
    /// tenant is A must only surface A's rows. Tenant B's row is invisible to A from
    /// two layers down — the application-level filter on the DbContext AND the
    /// SESSION_CONTEXT-driven SECURITY POLICY at the database layer.
    /// </summary>
    [Fact]
    public async Task Per_tenant_query_filter_scopes_results_to_current_tenant()
    {
        await EnsureMigratedAndClearOrdersAsync();

        // Insert one row per tenant. Each insert happens in its own scope after switching
        // the accessor so the SESSION_CONTEXT-driven RLS BLOCK PREDICATE on writes accepts
        // each row under its own tenant identity.
        var aOrderId = Guid.NewGuid();
        var bOrderId = Guid.NewGuid();

        _accessor.SwitchTo(TenantA);
        using (var aScope = _services.CreateScope())
        {
            var db = aScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            db.Orders.Add(NewOrder(aOrderId, TenantA, "ord_tenant_a"));
            await db.SaveChangesAsync();
        }

        _accessor.SwitchTo(TenantB);
        using (var bScope = _services.CreateScope())
        {
            var db = bScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            db.Orders.Add(NewOrder(bOrderId, TenantB, "ord_tenant_b"));
            await db.SaveChangesAsync();
        }

        // Now query while scoped to Tenant A and assert isolation.
        _accessor.SwitchTo(TenantA);
        using var readScope = _services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var visible = await readDb.Orders.AsNoTracking().ToListAsync();

        Assert.Single(visible);
        Assert.Equal(aOrderId, visible[0].Id);
        Assert.Equal(TenantA, visible[0].TenantId);
        Assert.DoesNotContain(visible, o => o.Id == bOrderId);
    }

    // --- helpers ----------------------------------------------------------------------

    /// <summary>
    /// Ensures migrations are applied and deletes every row from <c>polar_report_orders</c>
    /// so the test that calls this owns the table's state. xUnit does not guarantee test
    /// order within a class, so every test that asserts on row counts establishes its own
    /// clean baseline. Uses <see cref="DatabaseFacade.ExecuteSqlRawAsync"/> which runs
    /// outside the EF tracker — RLS BLOCK PREDICATE doesn't fire on raw DELETE because the
    /// current session is still a valid tenant.
    /// </summary>
    private async Task EnsureMigratedAndClearOrdersAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        await db.Database.MigrateAsync();
        // RLS FILTER PREDICATE makes DELETE only affect rows visible to the current tenant.
        // Switch through both tenants so both rows are removed regardless of insert order.
        _accessor.SwitchTo(TenantA);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM polar_report_orders;");
        _accessor.SwitchTo(TenantB);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM polar_report_orders;");
    }

    private static ReportOrderEntity NewOrder(Guid id, string tenantId, string polarOrderId) => new()
    {
        Id = id,
        TenantId = tenantId,
        PolarOrderId = polarOrderId,
        OrderNumber = $"INV-{polarOrderId}",
        CustomerId = $"cus_{tenantId}",
        Status = "paid",
        Amount = 1000L,
        TaxAmount = 100L,
        RefundedAmount = 0L,
        Currency = "USD",
        LineItemCount = 1,
        InvoiceUrl = null,
        CreatedAt = DateTimeOffset.UtcNow,
        FulfilledAt = null,
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

    /// <summary>Test stub: never grants cross-tenant access — the production default for any
    /// host that hasn't installed PolarSharp.MultiTenant.Identity.</summary>
    private sealed class NoCrossTenantContext : IAppMasterAdminCrossTenantContext
    {
        public bool IsAllowedCrossTenantAccess => false;
    }
}
