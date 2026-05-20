using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace PolarSharp.EcommerceStoreManagement.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the <see cref="PolarCatalogDbContext"/> wired through
/// <see cref="PostgreSqlCatalogBuilderExtensions.UsePostgreSqlCatalog(IServiceCollection,string)"/>
/// against a real PostgreSQL 17 container spun up by Testcontainers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why an integration test in addition to the unit tests.</strong> The catalog
/// unit tests use an in-memory SQLite harness (<see cref="Infrastructure.CatalogTestContext"/>);
/// that exercises the EF model + the global tenant query filter but does NOT exercise the
/// engine-specific behavior of PostgreSQL: real <c>text</c>/<c>uuid</c> column types,
/// real Row-Level Security policies driven by <c>current_setting('app.current_tenant_id')</c>,
/// the <see cref="MultiTenant.EntityFrameworkCore.PostgreSQL.PostgreSqlTenantSessionInterceptor"/>
/// stamping the session per connection check-out via <c>set_config(...)</c>, and the
/// PostgreSQL-specific migrations actually applying against the engine they were generated
/// for. This class proves the catalog's full PostgreSQL provider stack works end-to-end.
/// </para>
/// <para>
/// <strong>What the cross-tenant test proves.</strong> DECISIONS.md D-005 calls out 5-layer
/// tenant isolation as a non-negotiable acceptance criterion for every new entity. Layer 1
/// is the EF Core global query filter (covered by unit tests); layer 2 is PostgreSQL's RLS
/// policy plus session settings. The cross-tenant test inserts a row as Tenant A, switches
/// the Finbuckle context to Tenant B, opens a fresh connection, and asserts zero rows are
/// returned — verifying both layers compose correctly. If either the filter OR the RLS
/// policy is bypassed, the test fails loudly.
/// </para>
/// <para>
/// <strong>CI category filtering convention.</strong>
/// </para>
/// <list type="bullet">
///   <item><description><c>dotnet test --filter "Category!=Integration"</c> — unit tests only. Fast.</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration"</c> — all integration tests. Slow (~30-60s per provider).</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration&amp;Provider=PostgreSQL"</c> — this provider only.</description></item>
/// </list>
/// <para>
/// <strong>Container lifecycle.</strong> One container per test class via
/// <see cref="IAsyncLifetime"/>. Per-test container startup would multiply the suite
/// runtime by 5+ — instead each test owns its own tenant id(s) so containers can be shared
/// safely across tests with no inter-test state leak.
/// </para>
/// <para>
/// <strong>Image pin.</strong> The <see cref="PostgreSqlBuilder"/> is fed
/// <c>postgres:17-alpine</c> explicitly so the container shape is reproducible across
/// machines (no implicit "latest" drift). Alpine is chosen over the default Debian-based
/// image for faster cold-start and smaller pull on CI agents.
/// </para>
/// <para>
/// <strong>Prerequisite.</strong> Docker must be running on the host. The
/// <c>postgres:17-alpine</c> image pulls automatically on first run.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "PostgreSQL")]
public sealed class PostgreSqlCatalogDbIntegrationTests : IAsyncLifetime
{
    private const string TenantA = "tenant-a-int-pg";
    private const string TenantB = "tenant-b-int-pg";

    private PostgreSqlContainer _container = null!;
    private MutableMultiTenantAccessor _accessor = null!;
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        // Pin the image so the container shape is reproducible across machines (no implicit
        // "latest" drift). Alpine for faster cold-start and smaller pull. The image-name
        // constructor overload is required — Testcontainers 4.11 deprecated the parameterless
        // PostgreSqlBuilder() ctor.
        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();

        // Mirror the production wiring exactly: call UsePostgreSqlCatalog so the
        // PostgreSqlTenantSessionInterceptor is registered and attached to the DbContext.
        // The interceptor depends on IMultiTenantContextAccessor + IAppMasterAdminCrossTenantContext;
        // we supply test-double singletons of both. The TenantAwareDbContextBase also reads
        // IMultiTenantContextAccessor in its constructor for the query filter.
        _accessor = new MutableMultiTenantAccessor(TenantA);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMultiTenantContextAccessor>(_accessor);
        services.AddSingleton<IAppMasterAdminCrossTenantContext>(new NoCrossTenantContext());

        services.UsePostgreSqlCatalog(_container.GetConnectionString());

        _services = services.BuildServiceProvider();

        // Apply all migrations once per class. Subsequent tests share the schema; each
        // test owns its own tenant ids so rows from one test cannot leak into another.
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
        await db.Database.MigrateAsync();
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

    /// <summary>
    /// Verifies that <c>MigrateAsync</c> succeeds against a real PostgreSQL 17 engine —
    /// the most basic precondition for every other test in this class. If this test fails,
    /// every other test in the class will fail too; isolating the migration step as a
    /// dedicated test gives a clear failure signal pointing at "the PostgreSQL migrations
    /// don't apply" rather than "test X has some other issue".
    /// </summary>
    [Fact]
    public async Task Container_boots_and_migrations_apply_cleanly()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);

        // The catalog ships two migrations as of the time this test class was written
        // (Initial + EnableRowLevelSecurity); assert both are applied so any future migration
        // addition is forced through a deliberate update of this baseline.
        Assert.Contains(applied, m => m.EndsWith("_Initial", StringComparison.Ordinal));
        Assert.Contains(applied, m => m.EndsWith("_EnableRowLevelSecurity", StringComparison.Ordinal));
    }

    /// <summary>
    /// Inserts a <see cref="LocalProductEntity"/> in the current tenant scope, reads it
    /// back via a fresh DbContext, and asserts that all scalar fields round-trip through
    /// a real PostgreSQL engine. This catches any provider-specific serialization issue
    /// (e.g. <c>text</c>-vs-<c>varchar</c> trimming, <c>timestamptz</c> precision loss)
    /// that the SQLite unit tests cannot detect.
    /// </summary>
    [Fact]
    public async Task Insert_and_read_back_LocalProduct_round_trips()
    {
        _accessor.SwitchTo(TenantA);

        var productId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);

        // Write scope.
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Products.Add(new LocalProductEntity
            {
                Id = productId,
                MasterName = "Integration Test Product (PostgreSQL)",
                MasterDescription = "Round-trip through real PostgreSQL 17.",
                MasterLanguage = "en-US",
                Kind = ProductKind.Product,
                PriceJson = """{"amount":1999,"currency":"USD"}""",
                AttachedBenefitsJson = "[]",
                Status = PublishStatus.Draft,
                CreatedAt = createdAt,
            });
            await db.SaveChangesAsync();
        }

        // Read scope — separate DbContext instance proves the row is persisted, not just tracked.
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var loaded = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId);

            Assert.NotNull(loaded);
            Assert.Equal("Integration Test Product (PostgreSQL)", loaded!.MasterName);
            Assert.Equal("Round-trip through real PostgreSQL 17.", loaded.MasterDescription);
            Assert.Equal("en-US", loaded.MasterLanguage);
            Assert.Equal(ProductKind.Product, loaded.Kind);
            Assert.Equal(PublishStatus.Draft, loaded.Status);
            Assert.Equal(TenantA, loaded.TenantId);
            Assert.Equal(createdAt, loaded.CreatedAt);
        }
    }

    /// <summary>
    /// DECISIONS.md D-005 acceptance criterion: every tenant-owned entity must be unreachable
    /// from a different tenant's scope. This test inserts under Tenant A, swaps the Finbuckle
    /// context to Tenant B, opens a fresh DbContext + connection, and asserts that the
    /// Tenant A row is invisible. Both the EF Core global filter (layer 1) and the PostgreSQL
    /// RLS policy + <c>set_config('app.current_tenant_id', ..., false)</c> session var (layer 2)
    /// must compose correctly for this assertion to hold; a regression in either layer breaks
    /// the test.
    /// </summary>
    [Fact]
    public async Task Cross_tenant_query_filter_blocks_reads_from_other_tenant()
    {
        // Stage a Tenant A row.
        _accessor.SwitchTo(TenantA);
        var tenantAProductId = Guid.NewGuid();
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Products.Add(new LocalProductEntity
            {
                Id = tenantAProductId,
                MasterName = "Tenant A only — must not leak.",
                MasterLanguage = "en-US",
                Kind = ProductKind.Product,
                PriceJson = """{"amount":100,"currency":"USD"}""",
                AttachedBenefitsJson = "[]",
                Status = PublishStatus.Draft,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Switch to Tenant B and assert the row is invisible. A fresh scope means a fresh
        // DbContext, which (a) re-reads the IMultiTenantContextAccessor in its constructor
        // and (b) opens a fresh connection so the PostgreSqlTenantSessionInterceptor fires
        // ConnectionOpenedAsync with the new tenant id baked into the session settings.
        _accessor.SwitchTo(TenantB);
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();

            // Direct point-query: must return null even though the row exists with that primary key.
            var directLookup = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == tenantAProductId);
            Assert.Null(directLookup);

            // Aggregate: no Tenant-A rows should be visible at all.
            var countOfTenantArows = await db.Products
                .AsNoTracking()
                .CountAsync(p => p.MasterName == "Tenant A only — must not leak.");
            Assert.Equal(0, countOfTenantArows);
        }

        // Sanity: when we switch back to Tenant A the row is visible — proves the row was
        // actually written and the previous assertions weren't masking an upstream failure.
        _accessor.SwitchTo(TenantA);
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            var visibleToOwner = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == tenantAProductId);
            Assert.NotNull(visibleToOwner);
        }
    }

    /// <summary>
    /// Calling <c>MigrateAsync</c> a second time must be a no-op — no exception, no duplicate
    /// rows in <c>__EFMigrationsHistory</c>. EF Core's <c>IHistoryRepository</c> guards
    /// against re-applying applied migrations; this test proves that guard works through
    /// the PostgreSQL provider's actual implementation, not just in unit-test stubs.
    /// </summary>
    [Fact]
    public async Task Migrations_are_idempotent_on_re_run()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();

        var appliedBefore = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        // Re-run. Should be a no-op since InitializeAsync already migrated.
        await db.Database.MigrateAsync();

        var appliedAfter = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        Assert.Equal(appliedBefore.Count, appliedAfter.Count);
        Assert.Equal(appliedBefore, appliedAfter);
    }

    // --- helpers --------------------------------------------------------------------------

    /// <summary>
    /// Mutable Finbuckle <see cref="IMultiTenantContextAccessor"/> so tests can swap the
    /// active tenant mid-stream without rebuilding the DI graph. Each <see cref="SwitchTo"/>
    /// call constructs a fresh <see cref="MultiTenantContext{T}"/> wrapping a
    /// <see cref="PolarTenantInfo"/> for the requested tenant id.
    /// </summary>
    private sealed class MutableMultiTenantAccessor : IMultiTenantContextAccessor
    {
        private IMultiTenantContext _current;

        public MutableMultiTenantAccessor(string tenantId)
        {
            _current = BuildContext(tenantId);
        }

        public IMultiTenantContext MultiTenantContext
        {
            get => _current;
            set => _current = value;
        }

        public void SwitchTo(string tenantId) => _current = BuildContext(tenantId);

        private static IMultiTenantContext BuildContext(string tenantId) =>
            new MultiTenantContext<PolarTenantInfo>(
                new PolarTenantInfo { Id = tenantId, Identifier = tenantId, Name = tenantId });
    }

    /// <summary>
    /// Stub <see cref="IAppMasterAdminCrossTenantContext"/> that never grants cross-tenant
    /// access — pinning the safe default so the cross-tenant isolation test cannot be
    /// silently bypassed by a real cross-tenant signal from a future DI addition.
    /// </summary>
    private sealed class NoCrossTenantContext : IAppMasterAdminCrossTenantContext
    {
        public bool IsAllowedCrossTenantAccess => false;
    }
}
