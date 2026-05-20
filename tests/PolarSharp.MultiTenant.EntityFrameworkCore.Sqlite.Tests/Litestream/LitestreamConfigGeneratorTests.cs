using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite;
using PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Tests.Litestream;

/// <summary>
/// Tests for <see cref="LitestreamConfigGenerator"/> — the YAML producer that builds
/// <c>litestream.yml</c> from the directory contents + resolved options.
/// </summary>
/// <remarks>
/// <para>
/// The generator's contract is "deterministic YAML given a directory + options instance".
/// Tests use plain string-contains assertions rather than a snapshot framework to keep the
/// test surface small and obvious, and to avoid checking in <c>.verified.txt</c> files for
/// every replica-target variant.
/// </para>
/// </remarks>
public sealed class LitestreamConfigGeneratorTests
{
    // --- dbs: array composition -------------------------------------------------------

    [Fact]
    public void Generate_includes_master_SaaS_db_in_dbs_array()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var sut = new LitestreamConfigGenerator();

        var yaml = sut.Generate(temp.Path, TestHelpers.FullyEnabledOptions());

        Assert.Contains(SqliteBuilderExtensions.MasterSaasFileName, yaml, StringComparison.Ordinal);
        // The dbs: header should appear exactly once.
        Assert.Contains("dbs:", yaml, StringComparison.Ordinal);
        // Should have exactly one db entry (path: ...).
        Assert.Equal(1, CountOccurrences(yaml, "  - path:"));
    }

    [Fact]
    public void Generate_includes_per_tenant_db_files()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var t3 = Guid.NewGuid();
        temp.TouchFile($"{t1}.db");
        temp.TouchFile($"{t2}.db");
        temp.TouchFile($"{t3}.db");
        var sut = new LitestreamConfigGenerator();

        var yaml = sut.Generate(temp.Path, TestHelpers.FullyEnabledOptions());

        Assert.Equal(4, CountOccurrences(yaml, "  - path:"));
        Assert.Contains(t1.ToString(), yaml, StringComparison.Ordinal);
        Assert.Contains(t2.ToString(), yaml, StringComparison.Ordinal);
        Assert.Contains(t3.ToString(), yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_omits_excluded_tenants()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var keep = Guid.NewGuid();
        var drop1 = Guid.NewGuid();
        var drop2 = Guid.NewGuid();
        temp.TouchFile($"{keep}.db");
        temp.TouchFile($"{drop1}.db");
        temp.TouchFile($"{drop2}.db");
        var sut = new LitestreamConfigGenerator();
        var excluded = new HashSet<Guid> { drop1, drop2 };

        var yaml = sut.Generate(temp.Path, TestHelpers.FullyEnabledOptions(), excluded);

        // 2 active entries: master + keep.
        Assert.Equal(2, CountOccurrences(yaml, "  - path:"));
        Assert.Contains(keep.ToString(), yaml, StringComparison.Ordinal);
        // 2 exclusion-comment lines.
        Assert.Equal(2, CountOccurrences(yaml, "# tenant "));
        Assert.Contains(drop1.ToString(), yaml, StringComparison.Ordinal); // appears in comment
        Assert.Contains(drop2.ToString(), yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_keeps_master_SaaS_db_regardless_of_exclusion_set()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var sut = new LitestreamConfigGenerator();
        // Pass a non-empty exclusion set; master_SaaS.db's stem is NOT a Guid so it is never
        // matched as an exclusion target.
        var excluded = new HashSet<Guid> { Guid.NewGuid() };

        var yaml = sut.Generate(temp.Path, TestHelpers.FullyEnabledOptions(), excluded);

        Assert.Contains(SqliteBuilderExtensions.MasterSaasFileName, yaml, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(yaml, "  - path:"));
    }

    [Fact]
    public void Generate_keeps_unparseable_filenames()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        temp.TouchFile("random_other_file.db");
        var sut = new LitestreamConfigGenerator();
        // Even with an exclusion set, non-Guid filenames are always included.
        var excluded = new HashSet<Guid> { Guid.NewGuid() };

        var yaml = sut.Generate(temp.Path, TestHelpers.FullyEnabledOptions(), excluded);

        Assert.Contains("random_other_file.db", yaml, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(yaml, "  - path:"));
    }

    // --- Per-target replica configs ---------------------------------------------------

    [Fact]
    public void Generate_emits_S3_replica_config_when_target_is_S3()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var opts = TestHelpers.FullyEnabledOptions();
        opts.S3.Bucket = "my-replica-bucket";
        opts.S3.Region = "eu-west-1";
        var sut = new LitestreamConfigGenerator();

        var yaml = sut.Generate(temp.Path, opts);

        Assert.Contains("type: s3", yaml, StringComparison.Ordinal);
        Assert.Contains("bucket: my-replica-bucket", yaml, StringComparison.Ordinal);
        Assert.Contains("region: eu-west-1", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_emits_AzureBlob_replica_config_when_target_is_AzureBlob()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.AzureBlob;
        opts.AzureBlob = new LitestreamAzureBlobOptions
        {
            AccountName = "polarstore",
            Container = "tenants",
            AccessKeyEnvVar = "AZURE_STORAGE_KEY",
        };
        var sut = new LitestreamConfigGenerator();

        var yaml = sut.Generate(temp.Path, opts);

        Assert.Contains("type: abs", yaml, StringComparison.Ordinal);
        Assert.Contains("account-name: polarstore", yaml, StringComparison.Ordinal);
        Assert.Contains("bucket: tenants", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_emits_GoogleCloudStorage_replica_config_when_target_is_GCS()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.GoogleCloudStorage;
        opts.GoogleCloudStorage = new LitestreamGoogleCloudStorageOptions
        {
            Bucket = "gcs-bucket",
            CredentialsJsonPath = "/etc/litestream/gcp.json",
        };
        var sut = new LitestreamConfigGenerator();

        var yaml = sut.Generate(temp.Path, opts);

        Assert.Contains("type: gcs", yaml, StringComparison.Ordinal);
        Assert.Contains("bucket: gcs-bucket", yaml, StringComparison.Ordinal);
        Assert.Contains("credentials-path: /etc/litestream/gcp.json", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_emits_Sftp_replica_config()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.Sftp;
        opts.Sftp = new LitestreamSftpOptions
        {
            Host = "backup.example.com",
            Port = 2222,
            User = "polar",
            Path = "/srv/polarsharp/tenants/",
            PrivateKeyPath = "/etc/ssh/id_rsa",
        };
        var sut = new LitestreamConfigGenerator();

        var yaml = sut.Generate(temp.Path, opts);

        Assert.Contains("type: sftp", yaml, StringComparison.Ordinal);
        Assert.Contains("host: backup.example.com:2222", yaml, StringComparison.Ordinal);
        Assert.Contains("user: polar", yaml, StringComparison.Ordinal);
        Assert.Contains("key-path: /etc/ssh/id_rsa", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_emits_LocalDisk_replica_config()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var opts = TestHelpers.FullyEnabledOptions();
        opts.ReplicaTargetType = LitestreamReplicaTargetType.LocalDisk;
        opts.LocalDisk = new LitestreamLocalDiskOptions { Path = "/var/lib/polar/replica" };
        var sut = new LitestreamConfigGenerator();

        var yaml = sut.Generate(temp.Path, opts);

        Assert.Contains("type: file", yaml, StringComparison.Ordinal);
        // The replica path joins LocalDisk.Path with the file-name-without-extension.
        Assert.Contains("/var/lib/polar/replica", yaml, StringComparison.Ordinal);
    }

    // --- Tuning + metrics knobs --------------------------------------------------------

    [Fact]
    public void Generate_includes_sync_snapshot_retention_settings()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var opts = TestHelpers.FullyEnabledOptions();
        opts.SyncIntervalSeconds = 7;
        opts.SnapshotIntervalMinutes = 45;
        opts.RetentionDays = 14;
        var sut = new LitestreamConfigGenerator();

        var yaml = sut.Generate(temp.Path, opts);

        Assert.Contains("sync-interval: 7s", yaml, StringComparison.Ordinal);
        Assert.Contains("snapshot-interval: 45m", yaml, StringComparison.Ordinal);
        // Retention is emitted as hours (days × 24) because Litestream's YAML parser
        // calls Go's time.ParseDuration, which understands h/m/s but NOT 'd'. 14 days = 336h.
        // See Phase 2e fix in LitestreamConfigGenerator.cs.
        Assert.Contains("retention: 336h", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_includes_metrics_endpoint_addr()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var opts = TestHelpers.FullyEnabledOptions(metricsPort: 9191);
        var sut = new LitestreamConfigGenerator();

        var yaml = sut.Generate(temp.Path, opts);

        Assert.Contains("addr: \":9191\"", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_per_file_path_prefix_per_replica_target()
    {
        using var temp = new TempDirectoryFixture();
        temp.TouchFile(SqliteBuilderExtensions.MasterSaasFileName);
        var tenant = Guid.NewGuid();
        temp.TouchFile($"{tenant}.db");
        var opts = TestHelpers.FullyEnabledOptions();
        opts.S3.PathPrefix = "polarsharp/tenants/";
        var sut = new LitestreamConfigGenerator();

        var yaml = sut.Generate(temp.Path, opts);

        // The master file's replica path key carries the master stem.
        Assert.Contains("polarsharp/tenants/master_SaaS", yaml, StringComparison.Ordinal);
        // The tenant file's replica path key carries the tenant Guid stem.
        Assert.Contains($"polarsharp/tenants/{tenant}", yaml, StringComparison.Ordinal);
    }

    // --- Guard clauses ----------------------------------------------------------------

    [Fact]
    public void Generate_throws_InvalidOperationException_when_UseLitestream_is_false()
    {
        using var temp = new TempDirectoryFixture();
        var opts = TestHelpers.FullyEnabledOptions();
        opts.UseLitestream = false;
        var sut = new LitestreamConfigGenerator();

        Assert.Throws<InvalidOperationException>(() => sut.Generate(temp.Path, opts));
    }

    // --- Helpers ----------------------------------------------------------------------

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return 0;
        int count = 0;
        int idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
