using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb;
using PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb.Upgrade;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;
using Testcontainers.MariaDb;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// End-to-end integration tests for <see cref="MariaDbSingleTenantUpgradeMigrator"/>
/// against a real MariaDB 11 container spun up by Testcontainers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why an integration test in addition to the unit tests.</strong> The MariaDB
/// migrator drives real EF Core migrations, real transactional <c>BEGIN/COMMIT</c> against
/// a real engine, bulk <c>UPDATE</c> statements via raw ADO.NET, and real
/// <c>polar_upgrade_history</c> row writes. None of those code paths are exercised by the
/// in-memory unit tests under <c>tests/.../Upgrade/</c> — those use stubs. This class
/// proves the migrator's happy path works end-to-end against the engine it is built for.
/// </para>
/// <para>
/// <strong>Phase 2c scope.</strong> The <see cref="PolarTenantDbContext"/> itself has zero
/// <see cref="ITenantOwned"/> entities (it is the registry, not application data), so the
/// migrator's backfill loop finds nothing to stamp. That is fine for Phase 2c: the value
/// here is proving that the Testcontainers harness works against MariaDB, that migrations
/// apply against a real engine, that the completion marker is written transactionally, and
/// that re-runs are idempotent.
/// </para>
/// <para>
/// <strong>Why MariaDB is simpler than the other two providers.</strong> MariaDB / MySQL do
/// not expose Postgres-style <c>ROW LEVEL SECURITY</c> nor SQL Server-style
/// <c>SESSION_CONTEXT</c>-driven policies, so the migrator does not have to manipulate any
/// session variables to bypass a DB-layer guard. The backfill is just plain
/// <c>UPDATE ... SET TenantId = ?</c> statements inside a transaction. The integration test
/// therefore only has to verify the happy path — there is no RLS-bypass branch to cover.
/// </para>
/// <para>
/// <strong>CI category filtering convention</strong> (established by Phase 2a):
/// </para>
/// <list type="bullet">
///   <item><description><c>dotnet test</c> — runs <em>all</em> tests including integration tests. Slow.</description></item>
///   <item><description><c>dotnet test --filter "Category!=Integration"</c> — unit tests only. Fast (~seconds).</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration"</c> — integration tests only. Slow (~30–60s per provider).</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration&amp;Provider=MariaDb"</c> — this provider only.</description></item>
/// </list>
/// <para>
/// <strong>Container lifecycle.</strong> One container per test class via
/// <see cref="IAsyncLifetime"/>. Container startup is ~10–20s; the full class runs in
/// ~30–45s. Tests must be order-independent because xUnit does not guarantee execution
/// order within a class: every test that asserts on <c>polar_upgrade_history</c> state
/// clears the table at the start of the test so it owns its own state.
/// </para>
/// <para>
/// <strong>Image pin.</strong> The <see cref="MariaDbBuilder"/> is fed <c>mariadb:11.4</c>
/// deliberately — 11.4 is the current MariaDB LTS line (Long Term Support, maintained
/// through 2029) and pinning an explicit tag keeps the container shape reproducible across
/// machines (no implicit "latest" drift).
/// </para>
/// <para>
/// <strong>Provider-specific note: MySQL EF Core provider.</strong> This project uses
/// Oracle's <c>MySql.EntityFrameworkCore</c> (the official Oracle build, not Pomelo's
/// community fork) because Pomelo had not yet shipped an EF Core 10 build at the time the
/// MariaDB package was authored. Oracle's provider exposes <c>UseMySQL</c> (note: capital
/// <c>SQL</c>) and — unlike Pomelo's <c>UseMySql</c> — does NOT require a
/// <c>ServerVersion</c> parameter. That keeps the wire-up here a single
/// <c>opts.UseMySQL(connectionString, mysql =&gt; mysql.MigrationsAssembly(...))</c> call,
/// matching the production registration in <see cref="MariaDbBuilderExtensions.UseMariaDb"/>.
/// </para>
/// <para>
/// <strong>Provider-specific workaround: migration lock vs. MariaDB.</strong> Oracle's
/// provider implements migration-lock acquisition by issuing
/// <c>SELECT GET_LOCK('__EFMigrationsLock', -1)</c>. On MySQL the negative timeout means
/// "wait forever" and returns the bigint <c>1</c>. On MariaDB the same call is documented
/// to return <c>NULL</c>, which the provider then tries to cast to <see cref="long"/> and
/// throws <see cref="InvalidCastException"/>. The test harness replaces
/// <see cref="IHistoryRepository"/> with <see cref="MariaDbCompatibleHistoryRepository"/>
/// (a decorator that delegates everything to Oracle's internal repository EXCEPT the lock
/// acquisition, which becomes a no-op). The no-op is safe in a single-connection
/// integration test where no concurrent migrator is racing for the lock. Production hosts
/// are unaffected because they do not call <c>MigrateAsync</c> through PolarSharp wiring —
/// they apply migrations through their own tooling.
/// </para>
/// <para>
/// <strong>Prerequisite.</strong> Docker must be running on the host. The
/// <c>mariadb:11.4</c> image pulls automatically on first run.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "MariaDb")]
public sealed class MariaDbSingleTenantUpgradeMigratorIntegrationTests : IAsyncLifetime
{
    private MariaDbContainer _container = null!;
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        // Pin the image so the container shape is reproducible across machines (no implicit
        // "latest" drift). 11.4 is the current MariaDB LTS line — the long-support major
        // branch — so the test stays valid for years without needing periodic re-pinning.
        // The image-name constructor overload is required: Testcontainers 4.11 deprecated
        // the parameterless MariaDbBuilder().
        _container = new MariaDbBuilder("mariadb:11.4")
            .Build();
        await _container.StartAsync();

        // Build a DI graph that mirrors what UseMariaDb() wires up in production. There is
        // no MariaDB equivalent of the SqlServerTenantSessionInterceptor / PostgreSqlTenantSessionInterceptor
        // because MariaDB has no DB-layer tenant isolation to feed session state into — the
        // global query filter on the DbContext alone enforces tenancy on this provider.
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<PolarTenantDbContext>(opts =>
        {
            opts.UseMySQL(
                _container.GetConnectionString(),
                mysql => mysql.MigrationsAssembly(typeof(MariaDbBuilderExtensions).Assembly.GetName().Name));
            // Workaround for Oracle MySql.EntityFrameworkCore 10.0.7 + MariaDB compatibility.
            // The provider's MySQLHistoryRepository.AcquireDatabaseLockAsync issues
            // SELECT GET_LOCK('__EFMigrationsLock', -1). On MySQL a negative timeout means
            // "wait forever" and returns 1; on MariaDB a negative timeout is documented to
            // return NULL — and Oracle's provider then throws InvalidCastException trying to
            // cast DBNull to Int64. Replacing IHistoryRepository with a decorator that hands
            // out a no-op IMigrationsDatabaseLock keeps every other code path identical
            // (table creation, applied-migration reads, insert/delete scripts all delegate
            // to the underlying MySQLHistoryRepository) while skipping only the broken lock
            // acquisition. Safe in single-connection integration tests where no concurrent
            // migrator is racing us for the lock.
            opts.ReplaceService<IHistoryRepository, MariaDbCompatibleHistoryRepository>();
        });

        // EfMultiTenantStore needs an IPolarTenantCache — wire a real in-memory cache so the
        // migrator exercises the same code path that runs in production.
        services.AddSingleton<IPolarTenantCache>(new MemoryPolarTenantCache(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new PolarTenantCacheOptions())));
        services.AddScoped<IMultiTenantStore<PolarTenantInfo>, EfMultiTenantStore>();
        services.AddScoped<ITenantRegistryUpgrader, DefaultTenantRegistryUpgrader>();
        services.AddScoped<ISingleTenantUpgradeMigrator, MariaDbSingleTenantUpgradeMigrator>();

        _services = services.BuildServiceProvider();

        // Apply migrations once per class. All migrations from the MariaDB provider
        // (Initial + AddUpgradeHistoryTable + AddTenantLifecycleColumns) apply here.
        // See MariaDbCompatibleHistoryRepository for the reason the IHistoryRepository
        // service was replaced above.
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
    /// The migrator completes successfully against a real MariaDB even though
    /// <see cref="PolarTenantDbContext"/> declares zero <see cref="ITenantOwned"/>
    /// entities — the backfill loop is simply a no-op and the registry+history rows
    /// are still written.
    /// </summary>
    [Fact]
    public async Task RunAsync_completes_successfully_against_real_MariaDb_with_empty_registry()
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

/// <summary>
/// Test-only decorator for <see cref="IHistoryRepository"/> that delegates every operation
/// to Oracle's internal <c>MySQLHistoryRepository</c> EXCEPT the migration-lock acquisition,
/// which is replaced with a no-op.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why this exists.</strong> Oracle's <c>MySql.EntityFrameworkCore</c> 10.0.7
/// implements <c>AcquireDatabaseLockAsync</c> by issuing
/// <c>SELECT GET_LOCK('__EFMigrationsLock', -1)</c>. On MySQL a negative timeout means
/// "wait forever" and the function returns the bigint <c>1</c>. On MariaDB the function is
/// documented to return <c>NULL</c> for any negative timeout, so the provider's
/// <c>(long)scalar</c> cast throws <see cref="InvalidCastException"/> and the migration
/// never starts. The bug lives entirely in the Oracle provider's MySQL-only assumption —
/// production code under <see cref="MariaDbBuilderExtensions"/> never asks for a migration
/// lock because production hosts apply migrations through other tooling.
/// </para>
/// <para>
/// <strong>What we do here.</strong> Replace <see cref="IHistoryRepository"/> via
/// <c>DbContextOptionsBuilder.ReplaceService</c> so EF Core constructs an instance of this
/// type instead of <c>MySQLHistoryRepository</c>. We still need the Oracle-provider's
/// implementation for table creation, applied-migration reads, and insert/delete script
/// generation — so we resolve the original <c>MySQLHistoryRepository</c> via the dependency
/// container and delegate every method to it. Only <see cref="AcquireDatabaseLock"/> and
/// <see cref="AcquireDatabaseLockAsync"/> are overridden to return a no-op lock.
/// </para>
/// <para>
/// <strong>Why the no-op lock is safe in this test context.</strong> The integration test
/// holds a single <see cref="PolarTenantDbContext"/> at any time and no concurrent migrator
/// is running. The migration lock exists to serialise concurrent <c>MigrateAsync</c> calls;
/// in a one-shot test fixture there is nothing to serialise against.
/// </para>
/// </remarks>
internal sealed class MariaDbCompatibleHistoryRepository : IHistoryRepository
{
    private readonly IHistoryRepository _inner;

    /// <summary>Builds the decorator by activating Oracle's <c>MySQLHistoryRepository</c> manually.</summary>
    /// <param name="dependencies">EF Core history-repository dependencies (forwarded to the inner instance).</param>
    public MariaDbCompatibleHistoryRepository(HistoryRepositoryDependencies dependencies)
    {
        var mysqlHistoryRepositoryType = typeof(MySql.EntityFrameworkCore.Infrastructure.MySQLDbContextOptionsBuilder).Assembly
            .GetType("MySql.EntityFrameworkCore.Migrations.Internal.MySQLHistoryRepository")
            ?? throw new InvalidOperationException(
                "Could not locate MySql.EntityFrameworkCore.Migrations.Internal.MySQLHistoryRepository " +
                "via reflection — the Oracle MySql.EntityFrameworkCore package layout may have changed.");
        _inner = (IHistoryRepository)Activator.CreateInstance(mysqlHistoryRepositoryType, dependencies)!;
    }

    /// <inheritdoc/>
    public LockReleaseBehavior LockReleaseBehavior => _inner.LockReleaseBehavior;

    /// <inheritdoc/>
    public bool Exists() => _inner.Exists();

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
        => _inner.ExistsAsync(cancellationToken);

    /// <inheritdoc/>
    public void Create() => _inner.Create();

    /// <inheritdoc/>
    public Task CreateAsync(CancellationToken cancellationToken = default)
        => _inner.CreateAsync(cancellationToken);

    /// <inheritdoc/>
    public bool CreateIfNotExists() => _inner.CreateIfNotExists();

    /// <inheritdoc/>
    public Task<bool> CreateIfNotExistsAsync(CancellationToken cancellationToken = default)
        => _inner.CreateIfNotExistsAsync(cancellationToken);

    /// <inheritdoc/>
    public IReadOnlyList<HistoryRow> GetAppliedMigrations() => _inner.GetAppliedMigrations();

    /// <inheritdoc/>
    public Task<IReadOnlyList<HistoryRow>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
        => _inner.GetAppliedMigrationsAsync(cancellationToken);

    /// <inheritdoc/>
    public string GetCreateScript() => _inner.GetCreateScript();

    /// <inheritdoc/>
    public string GetCreateIfNotExistsScript() => _inner.GetCreateIfNotExistsScript();

    /// <inheritdoc/>
    public string GetInsertScript(HistoryRow row) => _inner.GetInsertScript(row);

    /// <inheritdoc/>
    public string GetDeleteScript(string migrationId) => _inner.GetDeleteScript(migrationId);

    /// <inheritdoc/>
    public string GetBeginIfNotExistsScript(string migrationId) => _inner.GetBeginIfNotExistsScript(migrationId);

    /// <inheritdoc/>
    public string GetBeginIfExistsScript(string migrationId) => _inner.GetBeginIfExistsScript(migrationId);

    /// <inheritdoc/>
    public string GetEndIfScript() => _inner.GetEndIfScript();

    /// <inheritdoc/>
    public IMigrationsDatabaseLock AcquireDatabaseLock() => new NoOpMigrationsDatabaseLock(this);

    /// <inheritdoc/>
    public Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IMigrationsDatabaseLock>(new NoOpMigrationsDatabaseLock(this));

    /// <summary>No-op migration lock — disposing it does nothing, reacquiring it returns itself.</summary>
    private sealed class NoOpMigrationsDatabaseLock(IHistoryRepository historyRepository) : IMigrationsDatabaseLock
    {
        public IHistoryRepository HistoryRepository { get; } = historyRepository;

        public IMigrationsDatabaseLock ReacquireIfNeeded(bool migrationsAcquired, bool? lockReacquired) => this;

        public Task<IMigrationsDatabaseLock> ReacquireIfNeededAsync(
            bool migrationsAcquired,
            bool? lockReacquired,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IMigrationsDatabaseLock>(this);

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
