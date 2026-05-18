using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb;

/// <summary>
/// Production <see cref="IHistoryRepository"/> decorator that works around an incompatibility
/// between Oracle's <c>MySql.EntityFrameworkCore</c> provider and MariaDB. The base
/// <c>MySQLHistoryRepository</c> acquires the EF migrations lock via
/// <c>SELECT GET_LOCK('__EFMigrationsLock', -1)</c>; on MySQL a -1 timeout means
/// "wait forever" and returns the bigint <c>1</c>, but on MariaDB the same call is documented
/// to return <c>NULL</c>, and the provider's <c>(long)scalar</c> cast then throws
/// <see cref="InvalidCastException"/>. The decorator substitutes a non-negative timeout
/// (<see cref="DefaultLockTimeoutSeconds"/> = 30 seconds) which returns <c>1</c> predictably on
/// BOTH MySQL and MariaDB, preserving the real lock semantics required for safe concurrent
/// migration of the same database from multiple processes.
/// </summary>
/// <remarks>
/// <para>
/// Registered via
/// <c>opts.ReplaceService&lt;IHistoryRepository, MariaDbCompatibleHistoryRepository&gt;()</c>
/// in each MariaDb provider package's <c>Use[X]MariaDb(...)</c> extension method. The five
/// MariaDb provider packages all share this single implementation by reference; the four
/// satellites (Identity, EcommerceStoreManagement, Reporting, PrepaidWallets) take a
/// <c>ProjectReference</c> on this package, so the type is reachable by name from each.
/// </para>
/// <para>
/// Removable as soon as <c>Pomelo.EntityFrameworkCore.MySql</c> ships a .NET 10 build (Pomelo's
/// repository never had the GET_LOCK-of-minus-one bug) OR Oracle fixes the upstream issue.
/// As of v1.3.x the latest stable Pomelo is 9.0.0 (EF Core 9 / .NET 9 only), so the entire
/// MariaDb provider family stays on Oracle's build and uses this decorator. Switch the
/// package family to Pomelo when its .NET 10 build lands; the decorator becomes unnecessary
/// at that point.
/// </para>
/// <para>
/// The decorator delegates every <see cref="IHistoryRepository"/> member to an inner instance
/// of Oracle's <c>MySQLHistoryRepository</c> EXCEPT <see cref="AcquireDatabaseLock"/> and
/// <see cref="AcquireDatabaseLockAsync"/>, which it implements directly against the
/// <see cref="IRelationalConnection"/> using a non-negative <c>GET_LOCK</c> timeout. Table
/// creation, applied-migration reads, insert/delete script generation all stay on Oracle's
/// implementation.
/// </para>
/// <para>
/// Reflection lookup of Oracle's internal type happens once per instance in the constructor.
/// The type lives in the <c>MySql.EntityFrameworkCore</c> assembly under the namespace
/// <c>MySql.EntityFrameworkCore.Migrations.Internal</c>, name <c>MySQLHistoryRepository</c>,
/// with a single public constructor taking
/// <see cref="HistoryRepositoryDependencies"/>. If Oracle restructures its internal layout in
/// a future release, the constructor throws <see cref="InvalidOperationException"/> with a
/// pointed message naming this file as the place to update.
/// </para>
/// </remarks>
public sealed class MariaDbCompatibleHistoryRepository : IHistoryRepository
{
    /// <summary>
    /// Default <c>GET_LOCK</c> timeout (seconds) used by the MariaDB compatibility shim.
    /// Any non-negative value returns the bigint <c>1</c> on both MySQL and MariaDB on
    /// success; only a negative value triggers the MariaDB-NULL bug. Thirty seconds matches
    /// the EF Core default migration command timeout shape and is generous enough that
    /// transient contention between concurrent migrators won't false-fail in production.
    /// </summary>
    public const int DefaultLockTimeoutSeconds = 30;

    /// <summary>
    /// The literal EF Core migrations-lock name, identical to the constant Oracle's provider
    /// uses internally. Keeping this in sync with Oracle's value matters: if two processes
    /// pick different lock names they will not actually serialise.
    /// </summary>
    private const string MigrationsLockName = "__EFMigrationsLock";

    private readonly IHistoryRepository _inner;
    private readonly HistoryRepositoryDependencies _dependencies;

    /// <summary>
    /// Builds the decorator. Constructs Oracle's <c>MySQLHistoryRepository</c> via reflection
    /// because the type is <c>internal</c> in Oracle's assembly and cannot be referenced
    /// directly.
    /// </summary>
    /// <param name="dependencies">EF Core history-repository dependencies, forwarded to the inner instance.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Oracle's internal <c>MySQLHistoryRepository</c> type cannot be located —
    /// the most likely cause is an Oracle release that restructured its internal namespaces,
    /// in which case the type name on the call to
    /// <see cref="Assembly.GetType(string)"/> below must be updated.
    /// </exception>
    public MariaDbCompatibleHistoryRepository(HistoryRepositoryDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        _dependencies = dependencies;

        var providerAssembly = typeof(MySql.EntityFrameworkCore.Infrastructure.MySQLDbContextOptionsBuilder).Assembly;
        var mysqlHistoryRepositoryType =
            providerAssembly.GetType("MySql.EntityFrameworkCore.Migrations.Internal.MySQLHistoryRepository")
            ?? throw new InvalidOperationException(
                "Could not locate MySql.EntityFrameworkCore.Migrations.Internal.MySQLHistoryRepository " +
                "via reflection. Either the Oracle MySql.EntityFrameworkCore package layout has changed " +
                "(update the type name in MariaDbCompatibleHistoryRepository) or the MySql.EntityFrameworkCore " +
                "package is missing from the dependency graph.");

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
    public IMigrationsDatabaseLock AcquireDatabaseLock()
    {
        var connection = _dependencies.Connection;
        EnsureConnectionOpen(connection);
        var command = BuildGetLockCommand(DefaultLockTimeoutSeconds);
        var result = command.ExecuteScalar(BuildParameterObject(connection));
        ValidateLockAcquired(result);
        return new GetLockReleaseDisposable(_dependencies, this);
    }

    /// <inheritdoc/>
    public async Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default)
    {
        var connection = _dependencies.Connection;
        await EnsureConnectionOpenAsync(connection, cancellationToken).ConfigureAwait(false);
        var command = BuildGetLockCommand(DefaultLockTimeoutSeconds);
        var result = await command
            .ExecuteScalarAsync(BuildParameterObject(connection), cancellationToken)
            .ConfigureAwait(false);
        ValidateLockAcquired(result);
        return new GetLockReleaseDisposable(_dependencies, this);
    }

    private IRelationalCommand BuildGetLockCommand(int timeoutSeconds)
        => _dependencies.RawSqlCommandBuilder.Build(
            string.Format(
                CultureInfo.InvariantCulture,
                "SELECT GET_LOCK('{0}', {1})",
                MigrationsLockName,
                timeoutSeconds));

    private IRelationalCommand BuildReleaseLockCommand()
        => _dependencies.RawSqlCommandBuilder.Build(
            string.Format(
                CultureInfo.InvariantCulture,
                "SELECT RELEASE_LOCK('{0}')",
                MigrationsLockName));

    private RelationalCommandParameterObject BuildParameterObject(IRelationalConnection connection)
        => new(
            connection,
            parameterValues: null,
            readerColumns: null,
            context: _dependencies.CurrentContext.Context,
            logger: _dependencies.CommandLogger);

    private static void EnsureConnectionOpen(IRelationalConnection connection)
    {
        if (connection.DbConnection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }
    }

    private static async ValueTask EnsureConnectionOpenAsync(IRelationalConnection connection, CancellationToken cancellationToken)
    {
        if (connection.DbConnection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidateLockAcquired(object? result)
    {
        // GET_LOCK returns 1 on success, 0 on timeout, NULL on error. On MySQL+MariaDB we
        // requested DefaultLockTimeoutSeconds, so we should always see 1 in a healthy
        // environment. Anything else is a hard failure to surface.
        if (result is null || result == DBNull.Value)
        {
            throw new InvalidOperationException(
                $"MariaDB-compatible EF migrations lock acquisition failed: GET_LOCK('{MigrationsLockName}', {DefaultLockTimeoutSeconds}) returned NULL. " +
                "This typically indicates a server-side error or a stale connection.");
        }

        var asLong = Convert.ToInt64(result, CultureInfo.InvariantCulture);
        if (asLong != 1L)
        {
            throw new InvalidOperationException(
                $"MariaDB-compatible EF migrations lock acquisition failed: GET_LOCK('{MigrationsLockName}', {DefaultLockTimeoutSeconds}) returned {asLong} (expected 1). " +
                "Another process is holding the migrations lock for longer than the configured timeout.");
        }
    }

    /// <summary>
    /// <see cref="IMigrationsDatabaseLock"/> implementation that issues
    /// <c>SELECT RELEASE_LOCK(...)</c> on <see cref="Dispose"/> /
    /// <see cref="DisposeAsync"/>. Re-acquire is a no-op because the underlying
    /// <c>GET_LOCK</c> is automatically valid for the duration of the session — EF Core
    /// only invokes re-acquire after a connection drop and reopen, which is rare during
    /// the brief migration window.
    /// </summary>
    private sealed class GetLockReleaseDisposable : IMigrationsDatabaseLock
    {
        private readonly HistoryRepositoryDependencies _dependencies;
        private readonly MariaDbCompatibleHistoryRepository _historyRepository;
        private bool _released;

        public GetLockReleaseDisposable(
            HistoryRepositoryDependencies dependencies,
            MariaDbCompatibleHistoryRepository historyRepository)
        {
            _dependencies = dependencies;
            _historyRepository = historyRepository;
        }

        public IHistoryRepository HistoryRepository => _historyRepository;

        public IMigrationsDatabaseLock ReacquireIfNeeded(bool migrationsAcquired, bool? lockReacquired)
            => this;

        public Task<IMigrationsDatabaseLock> ReacquireIfNeededAsync(
            bool migrationsAcquired,
            bool? lockReacquired,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IMigrationsDatabaseLock>(this);

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            try
            {
                var command = _historyRepository.BuildReleaseLockCommand();
                command.ExecuteScalar(_historyRepository.BuildParameterObject(_dependencies.Connection));
            }
            catch
            {
                // Best-effort release. Surfacing an exception from Dispose would mask the
                // real reason migrations failed (if any). The lock is also auto-released
                // when the connection closes, so a missed RELEASE_LOCK is harmless.
            }
        }

        public ValueTask DisposeAsync()
        {
            if (_released) return ValueTask.CompletedTask;
            _released = true;
            return new ValueTask(ReleaseAsync());
        }

        private async Task ReleaseAsync()
        {
            try
            {
                var command = _historyRepository.BuildReleaseLockCommand();
                await command
                    .ExecuteScalarAsync(_historyRepository.BuildParameterObject(_dependencies.Connection))
                    .ConfigureAwait(false);
            }
            catch
            {
                // Best-effort release (see Dispose comment).
            }
        }
    }
}
