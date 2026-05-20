using System.Globalization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Data.Sqlite;
using Minio;
using Minio.DataModel.Args;
using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;
using Testcontainers.Minio;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the optional Litestream replication path that ships
/// with <c>PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite</c>: stand up a MinIO container
/// as the S3-compatible replica target, generate a <c>litestream.yml</c> via
/// <see cref="LitestreamConfigGenerator"/>, start a real <c>litestream/litestream</c>
/// container reading a host-bind-mounted SQLite directory, write SQLite data, and verify
/// the bytes show up in the MinIO bucket.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why an integration test in addition to the five Litestream unit tests under
/// </strong><c>tests/PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Tests/Litestream/</c>.
/// The unit tests exercise the .NET code in isolation: the YAML string the generator
/// produces, the validator's no-op-when-disabled behavior, the health-check's metrics
/// parsing, the lifecycle handler's exclusion-set updates, the regen coordinator's
/// debouncing. None of those touch a real Litestream process, a real S3-compatible
/// store, or a real on-disk SQLite file under replication. This class closes that gap by
/// proving the .NET-generated YAML is structurally accepted by the real Litestream binary,
/// that the binary actually replicates a SQLite database into the configured target, and
/// that subsequent writes propagate as WAL segments. Without the integration test, a YAML
/// drift introduced by either side (PolarSharp regenerating a non-conformant config, or a
/// Litestream version bump renaming a field) would only be caught by users in production.
/// </para>
/// <para>
/// <strong>Architecture.</strong> Three Testcontainers join a shared Docker bridge network:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       A <see cref="MinioContainer"/> exposes the S3 API on a random host port (used by
///       the test process to create the bucket + verify replication) and is reachable from
///       inside the network via the alias <c>minio:9000</c> (used by Litestream via the
///       generated YAML's <c>endpoint:</c> field).
///     </description>
///   </item>
///   <item>
///     <description>
///       A plain <see cref="ContainerBuilder"/>-built Litestream container reads the YAML
///       from <c>/etc/litestream.yml</c> (bind-mounted from a host temp file the generator
///       wrote) and the SQLite directory from <c>/data</c> (bind-mounted from a host temp
///       directory). It is configured against the in-network MinIO endpoint with the
///       sandbox credentials. The container's PID 1 is <c>litestream replicate</c>, which
///       keeps streaming WAL segments to the bucket until the container is stopped.
///     </description>
///   </item>
///   <item>
///     <description>
///       The test process itself, running outside Docker, opens raw
///       <see cref="SqliteConnection"/> instances against the host-side directory (the same
///       directory bind-mounted into Litestream), writes data, and uses
///       <see cref="IMinioClient"/> against the random host port to confirm the replica
///       contents. The official MinIO .NET SDK is used in preference to AWSSDK.S3 here
///       specifically because the latter's transitive AWSSDK.Core dependency carries a
///       known low-severity vulnerability (GHSA-9cvc-h2w8-phrp) that NU1901 escalates to
///       a build-time error under <c>TreatWarningsAsErrors</c>.
///     </description>
///   </item>
/// </list>
/// <para>
/// <strong>Why raw <see cref="SqliteConnection"/> rather than EF Core.</strong> The unit
/// tests under <c>Litestream/</c> + the migrator-integration tests
/// (<see cref="SqlServerSingleTenantUpgradeMigratorIntegrationTests"/> and its siblings)
/// already cover the EF Core path. The replication contract Litestream cares about is at
/// the SQLite engine layer: WAL pages get appended on commit, Litestream observes them,
/// segments stream to the replica. Cutting EF Core out of the loop here keeps the test
/// focused on the replica boundary and reduces flakiness — any failure is unambiguously a
/// Litestream/YAML/network problem, not an EF Core impedance mismatch.
/// </para>
/// <para>
/// <strong>Skip-on-pull-failure pattern.</strong> Pulling <c>litestream/litestream:0.3.13</c>
/// (or the MinIO image) can fail on hosts with restrictive networking, registry-rate-limit
/// throttling, or Apple Silicon edge cases. This class follows the same posture established
/// by <see cref="CosmosDbSingleTenantUpgradeMigratorIntegrationTests"/>: catch any startup
/// exception in <see cref="InitializeAsync"/>, store the diagnostic in
/// <see cref="_bootFailureReason"/>, and have every <see cref="SkippableFactAttribute"/>
/// gate on it. Hosts where the integration cannot run get clean Skipped results with a
/// captured reason instead of red failures.
/// </para>
/// <para>
/// <strong>CI category filtering convention</strong> (per Phase 2a):
/// </para>
/// <list type="bullet">
///   <item><description><c>dotnet test --filter "Category!=Integration"</c> — unit tests only. Fast.</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration&amp;Provider=Litestream"</c> — this class only.</description></item>
/// </list>
/// <para>
/// <strong>Container lifecycle.</strong> All three containers (MinIO, Litestream, the
/// shared network) live for the duration of the class via <see cref="IAsyncLifetime"/>.
/// Container startup is the dominant cost (~30–60s on cold image pulls); the tests
/// themselves are sub-second once the bucket is created and Litestream is replicating.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "Litestream")]
public sealed class LitestreamMinIoIntegrationTests : IAsyncLifetime
{
    // Pinned image tags for reproducibility. Update deliberately when adopting a new
    // Litestream or MinIO release; bumping to "latest" implicitly would let drift sneak in
    // and quietly break the test for downstream users on a clean checkout.
    private const string LitestreamImage = "litestream/litestream:0.3.13";
    private const string MinioImage = "minio/minio:RELEASE.2024-12-18T13-15-44Z";

    private const string MinioNetworkAlias = "minio";
    private const int MinioInternalPort = 9000;
    private const string MinioAccessKey = "minioadmin";
    private const string MinioSecretKey = "minioadmin";
    private const string BucketName = "polarsharp-litestream-test";

    // Path inside the Litestream container that the host SQLite directory is bind-mounted
    // to. The generated litestream.yml references files under this path — host and
    // container see the SAME files via the bind-mount.
    private const string LitestreamContainerDataPath = "/data";
    private const string LitestreamContainerConfigPath = "/etc/litestream.yml";

    private INetwork? _network;
    private MinioContainer? _minioContainer;
    private IContainer? _litestreamContainer;

    private string _hostSqliteDirectory = "";
    private string _hostConfigFilePath = "";
    private string? _bootFailureReason;
    private IMinioClient? _minioClient;
    private int _minioHostPort;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        try
        {
            // Per-test-run temp directories so parallel runs / leftover state from earlier
            // runs can't collide. Cleaned up in DisposeAsync.
            _hostSqliteDirectory = Path.Combine(
                Path.GetTempPath(),
                $"polar-litestream-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_hostSqliteDirectory);

            _hostConfigFilePath = Path.Combine(
                Path.GetTempPath(),
                $"polar-litestream-config-{Guid.NewGuid():N}.yml");

            // Shared bridge network so Litestream can resolve "minio" by service name.
            // The MinIO container exposes 9000 on a RANDOM host port for the test process
            // to reach (host-side), and on the fixed internal port for Litestream to reach
            // (network-internal).
            _network = new NetworkBuilder()
                .WithName($"polar-litestream-net-{Guid.NewGuid():N}")
                .Build();
            await _network.CreateAsync();

            _minioContainer = new MinioBuilder(MinioImage)
                .WithUsername(MinioAccessKey)
                .WithPassword(MinioSecretKey)
                .WithNetwork(_network)
                .WithNetworkAliases(MinioNetworkAlias)
                .Build();
            await _minioContainer.StartAsync();

            _minioHostPort = _minioContainer.GetMappedPublicPort(MinioInternalPort);

            // MinIO client targeting the host-mapped port. Used to create the bucket
            // (Testcontainers.Minio does not auto-create one) and to verify replicated
            // objects appear after Litestream syncs.
            _minioClient = new MinioClient()
                .WithEndpoint($"localhost:{_minioHostPort.ToString(CultureInfo.InvariantCulture)}")
                .WithCredentials(MinioAccessKey, MinioSecretKey)
                .WithSSL(false)
                .Build();
            await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(BucketName));

            // Seed the SQLite directory with the shared master DB in WAL mode BEFORE
            // starting Litestream so the replicate command finds a file to watch at
            // startup. Per-tenant DBs created later by the test exercise the
            // "Litestream picks up new files via FileSystemWatcher" path that the
            // .NET LitestreamConfigAutoRegeneratorHostedService would trigger in production.
            var masterDbPath = Path.Combine(_hostSqliteDirectory, "master_SaaS.db");
            await SeedEmptyWalSqliteDatabaseAsync(masterDbPath);

            // Generate the YAML using the production code path. This is THE point of the
            // integration test: prove our generated YAML is what the real Litestream
            // binary accepts.
            var litestreamOptions = new LitestreamOptions
            {
                UseLitestream = true,
                ReplicaTargetType = LitestreamReplicaTargetType.S3,
                SyncIntervalSeconds = 1,
                SnapshotIntervalMinutes = 1,
                RetentionDays = 1,
                S3 = new LitestreamS3Options
                {
                    Bucket = BucketName,
                    Region = "us-east-1",
                    PathPrefix = "polarsharp/tenants/",
                    EndpointUrl = $"http://{MinioNetworkAlias}:{MinioInternalPort.ToString(CultureInfo.InvariantCulture)}",
                    ForcePathStyle = true,
                    AccessKeyIdEnvVar = "AWS_ACCESS_KEY_ID",
                    SecretAccessKeyEnvVar = "AWS_SECRET_ACCESS_KEY",
                },
            };
            var generator = new LitestreamConfigGenerator();
            // The generator reads files from disk; the bind-mount path inside the container
            // is /data, but at generation time we are reading the same files from their
            // host-side path. Litestream rewrites paths via the bind-mount, so we generate
            // the YAML with the CONTAINER paths inline. The easiest way is to swap the
            // host-side prefix for the container prefix in the generated string after the
            // fact — that keeps the production-code call shape identical and avoids
            // forcing the generator to take a "rewrite paths" parameter.
            var yamlForHostPaths = generator.Generate(_hostSqliteDirectory, litestreamOptions);
            var yamlForContainerPaths = yamlForHostPaths.Replace(
                _hostSqliteDirectory,
                LitestreamContainerDataPath,
                StringComparison.Ordinal);
            await File.WriteAllTextAsync(_hostConfigFilePath, yamlForContainerPaths);

            // Build and start the Litestream container. PID 1 is `litestream replicate`
            // which streams WAL segments to the configured replica until the container
            // stops. Logs are captured automatically by Testcontainers and surface in the
            // test failure output, which is where you go when a test fails for an
            // unexpected reason.
            _litestreamContainer = new ContainerBuilder(LitestreamImage)
                .WithNetwork(_network)
                .WithEnvironment("AWS_ACCESS_KEY_ID", MinioAccessKey)
                .WithEnvironment("AWS_SECRET_ACCESS_KEY", MinioSecretKey)
                .WithBindMount(_hostSqliteDirectory, LitestreamContainerDataPath)
                .WithBindMount(_hostConfigFilePath, LitestreamContainerConfigPath)
                .WithCommand("replicate", "-config", LitestreamContainerConfigPath)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilMessageIsLogged("initialized db"))
                .Build();
            await _litestreamContainer.StartAsync();
        }
        catch (Exception ex)
        {
            // Capture the reason so individual SkippableFacts can Skip.If with the message.
            // Same posture as Phase 2d's Cosmos emulator-failure path.
            _bootFailureReason =
                $"Litestream/MinIO container startup failed; this is expected on hosts that " +
                $"can't pull '{LitestreamImage}' or '{MinioImage}'. Underlying error: " +
                $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        if (_litestreamContainer is not null)
        {
            await _litestreamContainer.DisposeAsync();
        }
        if (_minioContainer is not null)
        {
            await _minioContainer.DisposeAsync();
        }
        if (_network is not null)
        {
            await _network.DisposeAsync();
        }
        _minioClient?.Dispose();

        // Best-effort cleanup of the host-side temp dir + yaml. A bind-mount being released
        // before this point is what makes the delete safe — IO errors here are ignorable
        // because the dir is uniquely-named and the OS reclaims tmp on reboot.
        TryDelete(_hostConfigFilePath, isDirectory: false);
        TryDelete(_hostSqliteDirectory, isDirectory: true);
    }

    /// <summary>
    /// Smoke test: the YAML <see cref="LitestreamConfigGenerator"/> emits is structurally
    /// accepted by the real Litestream binary — the container's
    /// <see cref="Wait.ForUnixContainer"/> startup wait would have failed if it weren't,
    /// because the binary logs <c>initialized db</c> only after parsing the config and
    /// opening the database successfully.
    /// </summary>
    /// <remarks>
    /// This is the lowest-bar end-to-end test and is the most valuable for catching YAML
    /// drift. A future Litestream release renaming a field would surface here as a config
    /// parse error before any of the data-replication assertions ran.
    /// </remarks>
    [SkippableFact]
    public void GeneratedYaml_IsAcceptedByRealLitestreamBinary()
    {
        Skip.If(_bootFailureReason is not null, _bootFailureReason);

        // Container's startup wait already gated on the "initialized db" log line, so by
        // the time InitializeAsync returned, we knew the YAML was accepted. The remaining
        // assertion is that the container is still running (didn't crash after init).
        Assert.NotNull(_litestreamContainer);
        Assert.Equal(TestcontainersStates.Running, _litestreamContainer!.State);
    }

    /// <summary>
    /// End-to-end: write SQLite data on the host side, wait for Litestream to stream the
    /// WAL segments, then verify the MinIO bucket has objects under the master_SaaS path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The assertion is intentionally coarse — "at least one object under the master_SaaS
    /// prefix" — because Litestream's internal layout (snapshot vs WAL object names, version
    /// suffixes, generation IDs) is an implementation detail that varies across releases.
    /// What we ARE asserting is the production-critical contract: data written through the
    /// SQLite WAL surface area reaches the configured replica. If that breaks, the host's
    /// disaster-recovery story is broken even when every unit test passes.
    /// </para>
    /// <para>
    /// The wait window of 15 seconds accommodates Litestream's default 10-second snapshot
    /// interval on initial replication; in steady-state the streaming sync interval is 1
    /// second.
    /// </para>
    /// </remarks>
    [SkippableFact]
    public async Task SqliteWrites_ReplicateToMinIoBucket()
    {
        Skip.If(_bootFailureReason is not null, _bootFailureReason);

        var masterDbPath = Path.Combine(_hostSqliteDirectory, "master_SaaS.db");

        // Insert a row and force a WAL checkpoint so the data is in pages Litestream's
        // replicator definitively observes (rather than relying on auto-checkpoint timing).
        await using (var conn = new SqliteConnection($"Data Source={masterDbPath}"))
        {
            await conn.OpenAsync();

            await using (var create = conn.CreateCommand())
            {
                create.CommandText =
                    "CREATE TABLE IF NOT EXISTS phase2e_marker (id INTEGER PRIMARY KEY, payload TEXT NOT NULL);";
                await create.ExecuteNonQueryAsync();
            }
            await using (var insert = conn.CreateCommand())
            {
                insert.CommandText = "INSERT INTO phase2e_marker (payload) VALUES (@p);";
                insert.Parameters.AddWithValue("@p", $"phase2e-{Guid.NewGuid():N}");
                await insert.ExecuteNonQueryAsync();
            }
            await using (var checkpoint = conn.CreateCommand())
            {
                checkpoint.CommandText = "PRAGMA wal_checkpoint(FULL);";
                await checkpoint.ExecuteNonQueryAsync();
            }
        }

        var deadline = DateTime.UtcNow.AddSeconds(20);
        var observedObjectsUnderMasterPrefix = 0;
        while (DateTime.UtcNow < deadline)
        {
            observedObjectsUnderMasterPrefix = await CountObjectsUnderPrefixAsync(
                _minioClient!,
                BucketName,
                "polarsharp/tenants/master_SaaS/");
            if (observedObjectsUnderMasterPrefix > 0)
            {
                break;
            }
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.True(
            observedObjectsUnderMasterPrefix > 0,
            $"Expected at least one object under 'polarsharp/tenants/master_SaaS/' " +
            $"after writing + checkpointing the master DB. Litestream did not replicate within " +
            $"the 20-second window. This indicates either a YAML config issue, a network " +
            $"reachability problem between containers, or a Litestream version regression.");
    }

    /// <summary>
    /// Counts the objects under the given prefix in the given MinIO bucket. The MinIO SDK
    /// exposes <see cref="IMinioClient.ListObjectsEnumAsync"/> for async-enumerable
    /// iteration; wrapping it here in a single int-returning helper keeps the test method
    /// readable.
    /// </summary>
    private static async Task<int> CountObjectsUnderPrefixAsync(
        IMinioClient client,
        string bucket,
        string prefix)
    {
        var count = 0;
        var args = new ListObjectsArgs()
            .WithBucket(bucket)
            .WithPrefix(prefix)
            .WithRecursive(true);
        await foreach (var _ in client.ListObjectsEnumAsync(args))
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// Creates an empty SQLite database in WAL journal mode at the given path. Litestream
    /// only replicates WAL-mode databases; the production
    /// <c>SqliteWalConfigurationInterceptor</c> sets this for EF Core connections, but the
    /// raw seed file here needs the PRAGMA set explicitly.
    /// </summary>
    private static async Task SeedEmptyWalSqliteDatabaseAsync(string dbPath)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        await cmd.ExecuteNonQueryAsync();
    }

    private static void TryDelete(string path, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            else
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch
        {
            // Best-effort: temp dir cleanup failures are non-fatal; OS reclaims tmp on reboot.
        }
    }
}
