using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore;
using PolarSharp.Reporting.EntityFrameworkCore.Entities;
using PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL;
using Testcontainers.PostgreSql;

namespace PolarSharp.Reporting.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the PostgreSQL reporting provider variant
/// (<see cref="PostgreSqlReportingExtensions.UsePostgreSqlReporting"/>) against a real
/// PostgreSQL 17 container spun up by Testcontainers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why an integration test.</strong> The PostgreSQL reporting wiring drives a real
/// EF Core migration pipeline (Initial + ModelDriftCatchup + EnableRowLevelSecurity), a
/// production-shape <see cref="PostgreSqlTenantSessionInterceptor"/> that issues
/// <c>set_config('app.current_tenant_id', ..., false)</c> on every connection open, and
/// real Postgres <c>ROW LEVEL SECURITY</c> policies on every <c>ITenantOwned</c> snapshot
/// table. None of those code paths execute against the in-memory SQLite test harness —
/// SQLite has no <c>set_config</c> and no <c>ENABLE ROW LEVEL SECURITY</c>. This class
/// proves the reporting DbContext round-trips correctly against the engine it ships for.
/// </para>
/// <para>
/// <strong>CI category filtering convention</strong>:
/// </para>
/// <list type="bullet">
///   <item><description><c>dotnet test --filter "Category!=Integration"</c> — fast unit run.</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration"</c> — all integration tests.</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration&amp;Provider=PostgreSQL"</c> — this provider only.</description></item>
/// </list>
/// <para>
/// <strong>Container lifecycle.</strong> One container per test class via
/// <see cref="IAsyncLifetime"/>. PostgreSQL container startup is ~5–10s (alpine image);
/// the full class runs in ~15–30s. Tests are order-independent — every test that asserts
/// on snapshot rows clears its own table at the start.
/// </para>
/// <para>
/// <strong>Image pin.</strong> <c>postgres:17-alpine</c> is pinned deliberately. Alpine is
/// chosen over the default Debian-based image for faster cold-start, and pinning 17 (the
/// current stable major) keeps the container shape reproducible across machines.
/// </para>
/// <para>
/// <strong>Prerequisite.</strong> Docker must be running on the host.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "PostgreSQL")]
public sealed class PostgreSqlReportingDbIntegrationTests : IAsyncLifetime
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    private PostgreSqlContainer _container = null!;
    private MutableTenantAccessor _accessor = null!;
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        // Pin the image so the container shape is reproducible across machines (no implicit
        // "latest" drift). Alpine is chosen for faster cold-start than the default
        // Debian-based image. The image-name constructor overload is required —
        // Testcontainers 4.11 deprecated the parameterless PostgreSqlBuilder().
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .Build();
        await _container.StartAsync();

        _accessor = new MutableTenantAccessor(TenantA);

        // Wire the production extension method end-to-end. UsePostgreSqlReporting registers
        // the PostgreSqlTenantSessionInterceptor — which depends on
        // IMultiTenantContextAccessor + IAppMasterAdminCrossTenantContext — so the test
        // harness must register stubs for both before BuildServiceProvider. The interceptor
        // sets app.current_tenant_id on every connection check-out, and the
        // EnableRowLevelSecurity migration's POLICY filters every snapshot table by that
        // session value.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMultiTenantContextAccessor>(_accessor);
        services.AddSingleton<IAppMasterAdminCrossTenantContext>(new NoCrossTenantContext());
        services.UsePostgreSqlReporting(_container.GetConnectionString());

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
    /// Applying the PostgreSQL reporting migrations against the live container must
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
    /// the migration pipeline is idempotent on the PostgreSQL provider.
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
    /// Inserting a <see cref="ReportCustomerEntity"/> through the real PostgreSQL provider
    /// and re-reading it via a fresh scope must surface every field byte-identical. Covers
    /// Npgsql's native <c>timestamptz</c> + <c>bigint</c> mapping without the SQLite
    /// value-converter detour.
    /// </summary>
    [Fact]
    public async Task Snapshot_row_round_trips_via_real_provider()
    {
        await EnsureMigratedAndClearCustomersAsync();
        _accessor.SwitchTo(TenantA);

        var customerId = Guid.NewGuid();
        var firstOrderAt = new DateTimeOffset(2026, 1, 4, 9, 15, 0, TimeSpan.Zero);
        var lastOrderAt = new DateTimeOffset(2026, 5, 18, 21, 45, 30, TimeSpan.Zero);
        var createdAt = new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero);

        using (var writeScope = _services.CreateScope())
        {
            var db = writeScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            db.Customers.Add(new ReportCustomerEntity
            {
                Id = customerId,
                TenantId = TenantA,
                PolarCustomerId = "cus_round_trip_pg",
                Email = "round-trip@example.test",
                Name = "Round Trip Customer",
                OrderCount = 7,
                LifetimeValue = 5_000_000L,
                Currency = "USD",
                FirstOrderAt = firstOrderAt,
                LastOrderAt = lastOrderAt,
                CreatedAt = createdAt,
                IsFakeData = false,
            });
            await db.SaveChangesAsync();
        }

        using var readScope = _services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var roundTripped = await readDb.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);

        Assert.Equal(TenantA, roundTripped.TenantId);
        Assert.Equal("cus_round_trip_pg", roundTripped.PolarCustomerId);
        Assert.Equal("round-trip@example.test", roundTripped.Email);
        Assert.Equal("Round Trip Customer", roundTripped.Name);
        Assert.Equal(7, roundTripped.OrderCount);
        Assert.Equal(5_000_000L, roundTripped.LifetimeValue);
        Assert.Equal("USD", roundTripped.Currency);
        Assert.Equal(firstOrderAt, roundTripped.FirstOrderAt);
        Assert.Equal(lastOrderAt, roundTripped.LastOrderAt);
        Assert.Equal(createdAt, roundTripped.CreatedAt);
        Assert.False(roundTripped.IsFakeData);
    }

    // --- Per-tenant query filter ------------------------------------------------------

    /// <summary>
    /// With the EF Core global query filter AND PostgreSQL FORCE ROW LEVEL SECURITY both
    /// active, inserting customers for Tenant A and Tenant B and then querying while the
    /// current tenant is A must only surface A's rows. Tenant B's row is invisible to A
    /// from two layers of isolation.
    /// </summary>
    [Fact]
    public async Task Per_tenant_query_filter_scopes_results_to_current_tenant()
    {
        await EnsureMigratedAndClearCustomersAsync();

        var aCustomerId = Guid.NewGuid();
        var bCustomerId = Guid.NewGuid();

        _accessor.SwitchTo(TenantA);
        using (var aScope = _services.CreateScope())
        {
            var db = aScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            db.Customers.Add(NewCustomer(aCustomerId, TenantA, "cus_tenant_a"));
            await db.SaveChangesAsync();
        }

        _accessor.SwitchTo(TenantB);
        using (var bScope = _services.CreateScope())
        {
            var db = bScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
            db.Customers.Add(NewCustomer(bCustomerId, TenantB, "cus_tenant_b"));
            await db.SaveChangesAsync();
        }

        _accessor.SwitchTo(TenantA);
        using var readScope = _services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        var visible = await readDb.Customers.AsNoTracking().ToListAsync();

        Assert.Single(visible);
        Assert.Equal(aCustomerId, visible[0].Id);
        Assert.Equal(TenantA, visible[0].TenantId);
        Assert.DoesNotContain(visible, c => c.Id == bCustomerId);
    }

    // --- helpers ----------------------------------------------------------------------

    /// <summary>
    /// Ensures migrations are applied and clears <c>polar_report_customers</c> so the
    /// calling test owns the table's state. RLS FILTER PREDICATE means a raw DELETE only
    /// removes rows visible to the current session's tenant — switch through both tenants
    /// to clear everything regardless of insert order.
    /// </summary>
    private async Task EnsureMigratedAndClearCustomersAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarReportingDbContext>();
        await db.Database.MigrateAsync();
        _accessor.SwitchTo(TenantA);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM polar_report_customers;");
        _accessor.SwitchTo(TenantB);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM polar_report_customers;");
    }

    private static ReportCustomerEntity NewCustomer(Guid id, string tenantId, string polarCustomerId) => new()
    {
        Id = id,
        TenantId = tenantId,
        PolarCustomerId = polarCustomerId,
        Email = $"{polarCustomerId}@example.test",
        Name = $"Customer for {tenantId}",
        OrderCount = 1,
        LifetimeValue = 1000L,
        Currency = "USD",
        FirstOrderAt = DateTimeOffset.UtcNow.AddDays(-30),
        LastOrderAt = DateTimeOffset.UtcNow.AddDays(-1),
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-60),
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
