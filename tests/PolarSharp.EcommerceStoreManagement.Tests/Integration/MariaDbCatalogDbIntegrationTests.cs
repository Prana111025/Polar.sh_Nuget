using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Entities;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.MariaDb;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb;
using Testcontainers.MariaDb;

namespace PolarSharp.EcommerceStoreManagement.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the <see cref="PolarCatalogDbContext"/> wired through
/// <see cref="MariaDbCatalogBuilderExtensions.UseMariaDbCatalog(IServiceCollection,string)"/>
/// against a real MariaDB 11 container spun up by Testcontainers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why an integration test in addition to the unit tests.</strong> The catalog
/// unit tests use an in-memory SQLite harness (<see cref="Infrastructure.CatalogTestContext"/>);
/// that exercises the EF model + the global tenant query filter but does NOT exercise the
/// engine-specific behavior of MariaDB: real <c>varchar</c>/<c>longtext</c> column types,
/// real <c>datetime(6)</c> precision, the
/// <see cref="MariaDbCompatibleHistoryRepository"/> workaround for
/// <c>SELECT GET_LOCK('__EFMigrationsLock', -1)</c> returning NULL on MariaDB (vs. 1 on
/// MySQL), and the MariaDB-specific migrations actually applying against the engine they
/// were generated for. This class proves the catalog's full MariaDB provider stack works
/// end-to-end.
/// </para>
/// <para>
/// <strong>Why MariaDB is simpler than the other two providers.</strong> MariaDB / MySQL
/// do not expose Postgres-style <c>ROW LEVEL SECURITY</c> nor SQL Server-style
/// <c>SESSION_CONTEXT</c>-driven policies, so the catalog package does not wire any session
/// interceptor for this provider. Per-tenant isolation is enforced by the EF Core global
/// query filter alone (layer 1). The MariaDB <see cref="MariaDbCatalogBuilderExtensions"/>
/// documents this explicitly: hosts that require defense-in-depth at the DB layer should
/// choose Postgres or SQL Server instead.
/// </para>
/// <para>
/// <strong>What the cross-tenant test proves on MariaDB.</strong> DECISIONS.md D-005 calls
/// out 5-layer tenant isolation as a non-negotiable acceptance criterion. On MariaDB the
/// only DbContext-layer defence is the EF Core global query filter; this test verifies that
/// filter blocks cross-tenant reads even when the Finbuckle context is swapped mid-session.
/// A regression in <see cref="TenantAwareDbContextBase"/>'s filter expression — or in the
/// per-instance closure that re-parameterises the filter per DbContext — would fail this
/// test and surface immediately.
/// </para>
/// <para>
/// <strong>Critical wiring detail: <see cref="MariaDbCompatibleHistoryRepository"/>.</strong>
/// Oracle's MySQL EF Core provider implements migration-lock acquisition with
/// <c>SELECT GET_LOCK('__EFMigrationsLock', -1)</c>. On MariaDB the negative timeout returns
/// <c>NULL</c> rather than the bigint <c>1</c> the provider expects, throwing
/// <see cref="InvalidCastException"/> on the cast. The catalog's <c>UseMariaDbCatalog</c>
/// extension wires <c>opts.ReplaceService&lt;IHistoryRepository, MariaDbCompatibleHistoryRepository&gt;()</c>
/// to swap in a non-negative timeout that returns 1 on both engines. The very fact that
/// <c>MigrateAsync</c> succeeds in <see cref="InitializeAsync"/> is the proof that the
/// production fix is in place — if a future refactor strips that
/// <c>ReplaceService</c> call out of <c>UseMariaDbCatalog</c>, this test class will fail
/// loudly at first migration attempt.
/// </para>
/// <para>
/// <strong>CI category filtering convention.</strong>
/// </para>
/// <list type="bullet">
///   <item><description><c>dotnet test --filter "Category!=Integration"</c> — unit tests only. Fast.</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration"</c> — all integration tests. Slow (~30-60s per provider).</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration&amp;Provider=MariaDb"</c> — this provider only.</description></item>
/// </list>
/// <para>
/// <strong>Container lifecycle.</strong> One container per test class via
/// <see cref="IAsyncLifetime"/>. Per-test container startup would multiply the suite
/// runtime by 5+ — instead each test owns its own tenant id(s) so containers can be shared
/// safely across tests with no inter-test state leak.
/// </para>
/// <para>
/// <strong>Image pin.</strong> The <see cref="MariaDbBuilder"/> is fed <c>mariadb:11.5</c>
/// explicitly so the container shape is reproducible across machines (no implicit "latest"
/// drift). 11.5 is a current MariaDB release; pinning a specific tag keeps the engine
/// behavior stable across CI machines.
/// </para>
/// <para>
/// <strong>Prerequisite.</strong> Docker must be running on the host. The
/// <c>mariadb:11.5</c> image pulls automatically on first run.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "MariaDb")]
public sealed class MariaDbCatalogDbIntegrationTests : IAsyncLifetime
{
    private const string TenantA = "tenant-a-int-maria";
    private const string TenantB = "tenant-b-int-maria";

    private MariaDbContainer _container = null!;
    private MutableMultiTenantAccessor _accessor = null!;
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        // Pin the image so the container shape is reproducible across machines (no implicit
        // "latest" drift). The image-name constructor overload is required: Testcontainers
        // 4.11 deprecated the parameterless MariaDbBuilder() ctor.
        _container = new MariaDbBuilder("mariadb:11.5").Build();
        await _container.StartAsync();

        // Mirror the production wiring exactly: call UseMariaDbCatalog so the
        // MariaDbCompatibleHistoryRepository is registered via ReplaceService and migrations
        // can apply against MariaDB without hitting the GET_LOCK NULL-cast bug. There is NO
        // session interceptor on this provider (MariaDB has no DB-layer tenant isolation),
        // so we only need the IMultiTenantContextAccessor for the TenantAwareDbContextBase
        // constructor + global filter. We still register an IAppMasterAdminCrossTenantContext
        // for completeness even though the MariaDB stack does not consult it at the engine
        // level.
        _accessor = new MutableMultiTenantAccessor(TenantA);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMultiTenantContextAccessor>(_accessor);
        services.AddSingleton<IAppMasterAdminCrossTenantContext>(new NoCrossTenantContext());

        services.UseMariaDbCatalog(_container.GetConnectionString());

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
    /// Verifies that <c>MigrateAsync</c> succeeds against a real MariaDB 11 engine — the
    /// most basic precondition for every other test in this class. Implicitly proves that
    /// the <see cref="MariaDbCompatibleHistoryRepository"/> workaround is wired correctly
    /// via <c>UseMariaDbCatalog</c>: without that replacement,
    /// <c>SELECT GET_LOCK('__EFMigrationsLock', -1)</c> returns NULL on MariaDB and the
    /// migration runner throws <see cref="InvalidCastException"/>.
    /// </summary>
    [Fact]
    public async Task Container_boots_and_migrations_apply_cleanly()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);

        // The MariaDB provider ships a single consolidated Initial migration as of the time
        // this test class was written (no RLS step — MariaDB has no row-level security).
        // Assert that the Initial migration applied so any future migration addition is
        // forced through a deliberate update of this baseline.
        Assert.Contains(applied, m => m.EndsWith("_Initial", StringComparison.Ordinal));
    }

    /// <summary>
    /// Inserts a <see cref="LocalProductEntity"/> in the current tenant scope, reads it
    /// back via a fresh DbContext, and asserts that all scalar fields round-trip through
    /// a real MariaDB engine. This catches any provider-specific serialization issue
    /// (e.g. <c>longtext</c> truncation, <c>datetime(6)</c> precision rounding) that the
    /// SQLite unit tests cannot detect.
    /// </summary>
    [Fact]
    public async Task Insert_and_read_back_LocalProduct_round_trips()
    {
        _accessor.SwitchTo(TenantA);

        var productId = Guid.NewGuid();
        // MariaDB's default datetime(6) precision is microseconds. Use a value with zero
        // sub-second component so the round-trip assertion isn't sensitive to MariaDB's
        // microsecond truncation behavior across versions.
        var createdAt = new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);

        // Write scope.
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();
            db.Products.Add(new LocalProductEntity
            {
                Id = productId,
                MasterName = "Integration Test Product (MariaDb)",
                MasterDescription = "Round-trip through real MariaDB 11.",
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
            Assert.Equal("Integration Test Product (MariaDb)", loaded!.MasterName);
            Assert.Equal("Round-trip through real MariaDB 11.", loaded.MasterDescription);
            Assert.Equal("en-US", loaded.MasterLanguage);
            Assert.Equal(ProductKind.Product, loaded.Kind);
            Assert.Equal(PublishStatus.Draft, loaded.Status);
            Assert.Equal(TenantA, loaded.TenantId);
            Assert.Equal(createdAt, loaded.CreatedAt);
        }
    }

    /// <summary>
    /// DECISIONS.md D-005 acceptance criterion: every tenant-owned entity must be unreachable
    /// from a different tenant's scope. On MariaDB the only DbContext-layer defence is the
    /// EF Core global query filter (no DB-layer RLS), so this test specifically exercises
    /// the <see cref="TenantAwareDbContextBase"/> filter expression and its per-instance
    /// closure that re-parameterises the filter per DbContext. A regression that constant-folds
    /// the first-resolved tenant id into the cached model would break this test.
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
        // DbContext, which re-reads the IMultiTenantContextAccessor in its constructor —
        // the filter expression captures `this` so each DbContext instance gets its own
        // CurrentTenantId value at query-execution time.
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
    /// rows in <c>__EFMigrationsHistory</c>. On MariaDB this is doubly important because the
    /// second call also re-acquires GET_LOCK; the <see cref="MariaDbCompatibleHistoryRepository"/>
    /// must continue to return 1 on every invocation, not just the first.
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

    /// <summary>
    /// Verifies that the <see cref="MariaDbCompatibleHistoryRepository"/> GET_LOCK workaround
    /// is actually wired into the DbContext options. Inspects EF Core's resolved services to
    /// confirm <see cref="IHistoryRepository"/> is the compatible implementation rather than
    /// the default Oracle-MySQL one — guarding against a future refactor accidentally
    /// stripping the <c>ReplaceService</c> call out of <c>UseMariaDbCatalog</c>.
    /// </summary>
    [Fact]
    public void MariaDbCompatibleHistoryRepository_is_wired_via_UseMariaDbCatalog()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarCatalogDbContext>();

        // Resolve EF Core's internal IHistoryRepository service. If UseMariaDbCatalog stopped
        // calling opts.ReplaceService<IHistoryRepository, MariaDbCompatibleHistoryRepository>(),
        // this would resolve to Oracle's default MySQLHistoryRepository instead.
        var historyRepo = db.GetInfrastructure().GetService<IHistoryRepository>();
        Assert.IsType<MariaDbCompatibleHistoryRepository>(historyRepo);
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
