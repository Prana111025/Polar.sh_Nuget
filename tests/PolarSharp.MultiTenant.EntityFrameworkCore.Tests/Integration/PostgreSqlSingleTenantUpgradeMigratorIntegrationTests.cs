using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL;
using PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL.Upgrade;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;
using Testcontainers.PostgreSql;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// End-to-end integration tests for <see cref="PostgreSqlSingleTenantUpgradeMigrator"/>
/// against a real PostgreSQL 17 container spun up by Testcontainers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why an integration test in addition to the unit tests.</strong> The PostgreSQL
/// migrator drives a real <c>SET LOCAL app.is_app_master_admin = 'true'</c> session call,
/// real EF Core migrations, real transactional <c>BEGIN/COMMIT</c> against a real engine,
/// and real <c>polar_upgrade_history</c> row writes. None of those code paths are exercised
/// by the in-memory unit tests under <c>tests/.../Upgrade/</c> — those use stubs. This class
/// proves the migrator's happy path works end-to-end against the engine it is built for.
/// </para>
/// <para>
/// <strong>Phase 2b scope.</strong> The <see cref="PolarTenantDbContext"/> itself has zero
/// <see cref="ITenantOwned"/> entities (it is the registry, not application data), so the
/// migrator's backfill loop finds nothing to stamp. That is fine for Phase 2b: the value
/// here is proving that the Testcontainers harness works against Postgres, that migrations
/// apply against a real engine, that the completion marker is written transactionally, and
/// that re-runs are idempotent. A later phase (Phase 2.2 or onward) will add a test-only
/// <see cref="ITenantOwned"/> entity wired through a real RLS policy to prove the
/// <c>SET LOCAL app.is_app_master_admin</c> bypass actually permits backfill writes against
/// a forced row-level-security table.
/// </para>
/// <para>
/// <strong>CI category filtering convention</strong> (established by Phase 2a):
/// </para>
/// <list type="bullet">
///   <item><description><c>dotnet test</c> — runs <em>all</em> tests including integration tests. Slow.</description></item>
///   <item><description><c>dotnet test --filter "Category!=Integration"</c> — unit tests only. Fast (~seconds).</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration"</c> — integration tests only. Slow (~30–60s per provider).</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration&amp;Provider=PostgreSQL"</c> — this provider only.</description></item>
/// </list>
/// <para>
/// <strong>Container lifecycle.</strong> One container per test class via
/// <see cref="IAsyncLifetime"/>. Container startup is ~5–10s (the alpine image is small);
/// the full class runs in ~15–30s. Tests must be order-independent because xUnit does not
/// guarantee execution order within a class: every test that asserts on
/// <c>polar_upgrade_history</c> state clears the table at the start of the test so it owns
/// its own state.
/// </para>
/// <para>
/// <strong>Image pin.</strong> The <see cref="PostgreSqlBuilder"/> is fed
/// <c>postgres:17-alpine</c> deliberately — alpine is a smaller image with faster cold-start
/// than the default Debian-based <c>postgres:latest</c>, and pinning 17 (the current latest
/// stable major as of late 2025 / early 2026) keeps the container shape reproducible across
/// machines.
/// </para>
/// <para>
/// <strong>Prerequisite.</strong> Docker must be running on the host. The
/// <c>postgres:17-alpine</c> image pulls automatically on first run.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "PostgreSQL")]
public sealed class PostgreSqlSingleTenantUpgradeMigratorIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        // Pin the image so the container shape is reproducible across machines (no implicit
        // "latest" drift). Alpine is chosen over the default Debian-based image for faster
        // cold-start and smaller pull on CI agents. The image-name constructor overload is
        // required — Testcontainers 4.11 deprecated the parameterless PostgreSqlBuilder().
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .Build();
        await _container.StartAsync();

        // Build a DI graph that mirrors what UsePostgreSql() wires up in production, minus
        // the PostgreSqlTenantSessionInterceptor: that interceptor reads the current Finbuckle
        // tenant context, which is not relevant for the upgrade-migrator path (the migrator
        // sets its own SET LOCAL app.is_app_master_admin bypass via raw ADO.NET inside the
        // backfill transaction).
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<PolarTenantDbContext>(opts =>
            opts.UseNpgsql(
                _container.GetConnectionString(),
                npg => npg.MigrationsAssembly(typeof(PostgreSqlBuilderExtensions).Assembly.GetName().Name)));

        // EfMultiTenantStore needs an IPolarTenantCache — wire a real in-memory cache so the
        // migrator exercises the same code path that runs in production.
        services.AddSingleton<IPolarTenantCache>(new MemoryPolarTenantCache(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new PolarTenantCacheOptions())));
        services.AddScoped<IMultiTenantStore<PolarTenantInfo>, EfMultiTenantStore>();
        services.AddScoped<ITenantRegistryUpgrader, DefaultTenantRegistryUpgrader>();
        services.AddScoped<ISingleTenantUpgradeMigrator, PostgreSqlSingleTenantUpgradeMigrator>();

        _services = services.BuildServiceProvider();

        // Apply migrations once per class. All migrations from the PostgreSQL provider
        // (Initial + AddUpgradeHistoryTable + AddTenantLifecycleColumns) apply here.
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
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

    // --- HasUpgradeCompletedAsync ------------------------------------------------------

    /// <summary>
    /// On a freshly-migrated database with no completion marker rows, the migrator must
    /// report the upgrade as <em>not</em> completed.
    /// </summary>
    [Fact]
    public async Task HasUpgradeCompletedAsync_returns_false_on_fresh_database()
    {
        await ClearUpgradeHistoryAsync();

        using var scope = _services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<ISingleTenantUpgradeMigrator>();

        var completed = await migrator.HasUpgradeCompletedAsync(CancellationToken.None);

        Assert.False(completed);
    }

    /// <summary>
    /// After a successful <see cref="ISingleTenantUpgradeMigrator.RunAsync"/>, the
    /// completion marker is visible via <see cref="ISingleTenantUpgradeMigrator.HasUpgradeCompletedAsync"/>.
    /// </summary>
    [Fact]
    public async Task HasUpgradeCompletedAsync_returns_true_after_successful_RunAsync()
    {
        await ClearUpgradeHistoryAsync();
        var defaultTenant = NewDefaultTenant();

        using var scope = _services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<ISingleTenantUpgradeMigrator>();

        await migrator.RunAsync(defaultTenant, CancellationToken.None);
        var completed = await migrator.HasUpgradeCompletedAsync(CancellationToken.None);

        Assert.True(completed);
    }

    // --- RunAsync happy-path ------------------------------------------------------------

    /// <summary>
    /// The migrator completes successfully against a real PostgreSQL even though
    /// <see cref="PolarTenantDbContext"/> declares zero <see cref="ITenantOwned"/>
    /// entities — the backfill loop is simply a no-op and the registry+history rows
    /// are still written.
    /// </summary>
    [Fact]
    public async Task RunAsync_completes_successfully_against_real_PostgreSql_with_empty_registry()
    {
        await ClearUpgradeHistoryAsync();
        var defaultTenant = NewDefaultTenant();

        using var scope = _services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<ISingleTenantUpgradeMigrator>();

        var result = await migrator.RunAsync(defaultTenant, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.False(result.AlreadyComplete);
        Assert.Equal(0, result.RowsStamped);
        Assert.Empty(result.RowsStampedByEntityType);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    /// <summary>
    /// The migrator inserts the supplied default tenant into the registry on first run.
    /// </summary>
    [Fact]
    public async Task RunAsync_inserts_default_tenant_into_registry()
    {
        await ClearUpgradeHistoryAsync();
        var defaultTenant = NewDefaultTenant();

        using var scope = _services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<ISingleTenantUpgradeMigrator>();
        var store = scope.ServiceProvider.GetRequiredService<IMultiTenantStore<PolarTenantInfo>>();

        var runResult = await migrator.RunAsync(defaultTenant, CancellationToken.None);
        Assert.True(runResult.Success, runResult.Message);

        var resolved = await store.GetByIdentifierAsync(defaultTenant.Identifier);
        Assert.NotNull(resolved);
        Assert.Equal(defaultTenant.Identifier, resolved!.Identifier);
        Assert.Equal(defaultTenant.Id, resolved.Id);
    }

    /// <summary>
    /// The migrator writes a <c>polar_upgrade_history</c> row with the canonical fields:
    /// <c>SingleTenantToMultiTenant</c> kind, <c>system</c> actor, and a non-empty message.
    /// </summary>
    [Fact]
    public async Task RunAsync_writes_polar_upgrade_history_row()
    {
        await ClearUpgradeHistoryAsync();
        var defaultTenant = NewDefaultTenant();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        using var scope = _services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<ISingleTenantUpgradeMigrator>();
        await migrator.RunAsync(defaultTenant, CancellationToken.None);

        // Re-read with a fresh DbContext so the assertion exercises the persisted row, not
        // any in-memory tracking from the migrator's own context.
        using var verifyScope = _services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        var row = await db.UpgradeHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.UpgradeKind == UpgradeKinds.SingleTenantToMultiTenant);

        Assert.NotNull(row);
        Assert.Equal("system", row!.ActorUserId);
        Assert.NotNull(row.Message);
        Assert.NotEmpty(row.Message!);
        Assert.True(row.CompletedAt >= before,
            $"CompletedAt {row.CompletedAt:O} should be at or after the test start {before:O}.");
        Assert.NotNull(row.ResultSummaryJson);
        Assert.Contains("Actions", row.ResultSummaryJson!, StringComparison.Ordinal);
    }

    // --- Idempotency --------------------------------------------------------------------

    /// <summary>
    /// A second invocation of <see cref="ISingleTenantUpgradeMigrator.RunAsync"/> detects the
    /// completion marker written by the first run and returns immediately with
    /// <see cref="SingleTenantUpgradeResult.AlreadyComplete"/> set, without writing a second
    /// history row.
    /// </summary>
    [Fact]
    public async Task RunAsync_is_idempotent_on_re_run()
    {
        await ClearUpgradeHistoryAsync();
        var defaultTenant = NewDefaultTenant();

        using var scope = _services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<ISingleTenantUpgradeMigrator>();

        var firstRun = await migrator.RunAsync(defaultTenant, CancellationToken.None);
        var secondRun = await migrator.RunAsync(defaultTenant, CancellationToken.None);

        Assert.True(firstRun.Success, firstRun.Message);
        Assert.False(firstRun.AlreadyComplete);
        Assert.True(secondRun.Success, secondRun.Message);
        Assert.True(secondRun.AlreadyComplete);

        // Exactly one history row exists despite two RunAsync calls — proves the second
        // call short-circuited before reaching the InsertCompletionRowAsync branch.
        using var verifyScope = _services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        var count = await db.UpgradeHistory
            .AsNoTracking()
            .CountAsync(r => r.UpgradeKind == UpgradeKinds.SingleTenantToMultiTenant);
        Assert.Equal(1, count);
    }

    // --- helpers ------------------------------------------------------------------------

    /// <summary>
    /// Clears the <c>polar_upgrade_history</c> table so the test that calls this owns the
    /// initial state. xUnit does not guarantee test execution order within a class, so every
    /// test that asserts on completion-marker state must establish its own clean baseline
    /// rather than relying on test ordering.
    /// </summary>
    private async Task ClearUpgradeHistoryAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM polar_upgrade_history;");
    }

    /// <summary>
    /// Builds a fresh <see cref="PolarTenantInfo"/> with a unique identifier per call. Tests
    /// share the container instance, so unique identifiers keep the registry inserts from
    /// colliding on the unique <c>Identifier</c> index across test methods.
    /// </summary>
    private static PolarTenantInfo NewDefaultTenant() => new()
    {
        Id = Guid.NewGuid().ToString(),
        Identifier = $"default-{Guid.NewGuid():N}",
        Name = "Default Tenant (Integration Test)",
        PolarAccessToken = "polar_oat_integration_test",
        PolarOrganizationId = "org_integration_test",
        Server = PolarServer.Sandbox,
        SiteManagerEmail = "integration-test@example.com",
    };
}
