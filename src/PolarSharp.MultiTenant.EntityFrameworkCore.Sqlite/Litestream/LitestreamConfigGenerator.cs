using System.Globalization;
using System.Text;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

/// <summary>
/// Generates a <c>litestream.yml</c> configuration string from the resolved
/// <see cref="LitestreamOptions"/> and the SQLite database directory layout.
/// </summary>
/// <remarks>
/// <para>
/// The host typically writes the generated string to disk (e.g.
/// <c>/etc/litestream/litestream.yml</c>) and points the Litestream daemon at it via
/// <c>litestream replicate -config /etc/litestream/litestream.yml</c>. The generator
/// produces YAML conforming to the Litestream config schema documented at
/// <see href="https://litestream.io/reference/config/"/>.
/// </para>
/// <para>
/// Every <c>.db</c> file in the database directory becomes a <c>dbs:</c> entry — the
/// shared <c>master_SaaS.db</c> plus each per-tenant <c>{tenantId}.db</c>. Each entry
/// declares one replica targeting the configured
/// <see cref="LitestreamOptions.ReplicaTargetType"/>, with a per-file path prefix so
/// the master and each tenant land in distinct destinations on the replica:
/// </para>
/// <list type="bullet">
///   <item><c>master_SaaS.db</c> → <c>{PathPrefix}/master_SaaS/</c></item>
///   <item><c>{tenantId}.db</c> → <c>{PathPrefix}/{tenantId}/</c></item>
/// </list>
/// <para>
/// YAML is constructed via <see cref="StringBuilder"/> rather than a third-party
/// serializer to keep the package free of additional runtime dependencies. The schema
/// surface used here is small and stable.
/// </para>
/// </remarks>
public sealed class LitestreamConfigGenerator
{
    /// <summary>
    /// Generates a <c>litestream.yml</c> content string for the given database directory
    /// and resolved Litestream options.
    /// </summary>
    /// <param name="databaseDirectory">
    /// The SQLite database directory (where <c>master_SaaS.db</c> and per-tenant
    /// <c>{tenantId}.db</c> files live). Must be an existing directory.
    /// </param>
    /// <param name="options">
    /// The resolved Litestream options. Must have
    /// <see cref="LitestreamOptions.UseLitestream"/> set to <see langword="true"/>;
    /// otherwise an <see cref="InvalidOperationException"/> is thrown — generating a
    /// config from a disabled options instance is a programming error.
    /// </param>
    /// <returns>Valid YAML content matching Litestream's config schema.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="databaseDirectory"/> is null, empty, or does not exist.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="LitestreamOptions.UseLitestream"/> is <see langword="false"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// var generator = new LitestreamConfigGenerator();
    /// var yaml = generator.Generate("/var/lib/polarsharp/tenants/", options);
    /// File.WriteAllText("/etc/litestream/litestream.yml", yaml);
    /// </code>
    /// </example>
    public string Generate(string databaseDirectory, LitestreamOptions options)
        => Generate(databaseDirectory, options, excludeTenantIds: null);

    /// <summary>
    /// Generates a <c>litestream.yml</c> content string for the given database directory
    /// and resolved Litestream options, optionally excluding specified tenants from the
    /// generated config.
    /// </summary>
    /// <param name="databaseDirectory">
    /// The SQLite database directory (where <c>master_SaaS.db</c> and per-tenant
    /// <c>{tenantId}.db</c> files live). Must be an existing directory.
    /// </param>
    /// <param name="options">
    /// The resolved Litestream options. Must have
    /// <see cref="LitestreamOptions.UseLitestream"/> set to <see langword="true"/>;
    /// otherwise an <see cref="InvalidOperationException"/> is thrown.
    /// </param>
    /// <param name="excludeTenantIds">
    /// Optional set of tenant IDs to EXCLUDE from the generated config. Each excluded
    /// tenant's <c>{tenantId}.db</c> file (if present) is omitted from the <c>dbs:</c>
    /// array, so Litestream stops replicating it on the next config reload. Pass
    /// <see langword="null"/> or an empty set to include all <c>.db</c> files.
    /// </param>
    /// <returns>Valid YAML content matching Litestream's config schema.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="databaseDirectory"/> is null, empty, or does not exist.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="LitestreamOptions.UseLitestream"/> is <see langword="false"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// A <c>.db</c> file whose stem parses as a <see cref="Guid"/> that appears in
    /// <paramref name="excludeTenantIds"/> is omitted from the <c>dbs:</c> array, and a
    /// YAML comment is emitted in its would-be location noting the exclusion. Files whose
    /// stem does not parse as a <see cref="Guid"/> (e.g., <c>master_SaaS.db</c>) are
    /// always included regardless of the exclusion set.
    /// </para>
    /// <para>
    /// Per the Stage C design decision, existing snapshots on the replica target are NOT
    /// removed when a tenant is excluded — they remain subject to the configured
    /// <see cref="LitestreamOptions.RetentionDays"/>. Excluding a tenant only stops new
    /// WAL changes from being pushed.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var generator = new LitestreamConfigGenerator();
    /// var excluded = new HashSet&lt;Guid&gt; { Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6") };
    /// var yaml = generator.Generate("/var/lib/polarsharp/tenants/", options, excluded);
    /// File.WriteAllText("/etc/litestream/litestream.yml", yaml);
    /// </code>
    /// </example>
    public string Generate(string databaseDirectory, LitestreamOptions options, IReadOnlySet<Guid>? excludeTenantIds)
    {
        ArgumentException.ThrowIfNullOrEmpty(databaseDirectory);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.UseLitestream)
        {
            throw new InvalidOperationException(
                "Cannot generate a Litestream config from a disabled options instance " +
                "(LitestreamOptions.UseLitestream = false).");
        }

        if (!Directory.Exists(databaseDirectory))
        {
            throw new ArgumentException(
                $"Database directory '{databaseDirectory}' does not exist.",
                nameof(databaseDirectory));
        }

        var dbFiles = Directory.EnumerateFiles(databaseDirectory, "*.db", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        var sb = new StringBuilder();
        AppendHeader(sb, options);
        AppendDbs(sb, options, dbFiles, excludeTenantIds);
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, LitestreamOptions options)
    {
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"# Generated by PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream.");
        sb.AppendLine("# Do not edit by hand — regenerate via the polar-mt CLI or the host startup hook.");
        sb.AppendLine();
        sb.AppendLine("addr: \":" + options.MetricsPort.ToString(CultureInfo.InvariantCulture) + "\"");
        sb.AppendLine();
        sb.AppendLine("dbs:");
    }

    private static void AppendDbs(
        StringBuilder sb,
        LitestreamOptions options,
        IReadOnlyList<string> dbFiles,
        IReadOnlySet<Guid>? excludeTenantIds)
    {
        var hasExclusions = excludeTenantIds is { Count: > 0 };

        foreach (var dbFile in dbFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(dbFile);

            if (hasExclusions
                && Guid.TryParse(fileName, out var tenantId)
                && excludeTenantIds!.Contains(tenantId))
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"  # tenant {tenantId} excluded from replication (suspended/inactive/deleted)");
                continue;
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"  - path: {dbFile}");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"    sync-interval: {options.SyncIntervalSeconds.ToString(CultureInfo.InvariantCulture)}s");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"    snapshot-interval: {options.SnapshotIntervalMinutes.ToString(CultureInfo.InvariantCulture)}m");
            sb.AppendLine("    replicas:");
            AppendReplica(sb, options, fileName);
        }
    }

    private static void AppendReplica(StringBuilder sb, LitestreamOptions options, string fileNameWithoutExt)
    {
        // Litestream parses 'retention' via Go's time.ParseDuration, which understands
        // h / m / s / ms / us / ns but NOT 'd' (days). Emit the configured RetentionDays
        // as a multiple of hours so the YAML round-trips through the real binary
        // (RetentionDays=1 -> "24h", RetentionDays=30 -> "720h"). The earlier "Nd" form
        // produced a "cannot unmarshal !!str `1d` into time.Duration" error on Litestream
        // 0.3.13+, surfaced by the Phase 2e end-to-end integration test.
        var retention = (options.RetentionDays * 24).ToString(CultureInfo.InvariantCulture) + "h";
        switch (options.ReplicaTargetType)
        {
            case LitestreamReplicaTargetType.S3:
                AppendS3Replica(sb, options.S3, fileNameWithoutExt, retention);
                break;
            case LitestreamReplicaTargetType.AzureBlob:
                AppendAzureBlobReplica(sb, options.AzureBlob, fileNameWithoutExt, retention);
                break;
            case LitestreamReplicaTargetType.GoogleCloudStorage:
                AppendGcsReplica(sb, options.GoogleCloudStorage, fileNameWithoutExt, retention);
                break;
            case LitestreamReplicaTargetType.Sftp:
                AppendSftpReplica(sb, options.Sftp, fileNameWithoutExt, retention);
                break;
            case LitestreamReplicaTargetType.LocalDisk:
                AppendLocalDiskReplica(sb, options.LocalDisk, fileNameWithoutExt, retention);
                break;
        }
    }

    private static void AppendS3Replica(StringBuilder sb, LitestreamS3Options s3, string fileName, string retention)
    {
        sb.AppendLine("      - type: s3");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        bucket: {s3.Bucket}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        path: {CombinePrefix(s3.PathPrefix, fileName)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        region: {s3.Region}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        access-key-id: ${{{s3.AccessKeyIdEnvVar}}}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        secret-access-key: ${{{s3.SecretAccessKeyEnvVar}}}");
        if (!string.IsNullOrEmpty(s3.EndpointUrl))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        endpoint: {s3.EndpointUrl}");
        }
        if (s3.ForcePathStyle)
        {
            sb.AppendLine("        force-path-style: true");
        }
        sb.AppendLine(CultureInfo.InvariantCulture, $"        retention: {retention}");
    }

    private static void AppendAzureBlobReplica(StringBuilder sb, LitestreamAzureBlobOptions azure, string fileName, string retention)
    {
        sb.AppendLine("      - type: abs");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        account-name: {azure.AccountName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        account-key: ${{{azure.AccessKeyEnvVar}}}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        bucket: {azure.Container}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        path: {CombinePrefix(azure.PathPrefix, fileName)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        retention: {retention}");
    }

    private static void AppendGcsReplica(StringBuilder sb, LitestreamGoogleCloudStorageOptions gcs, string fileName, string retention)
    {
        sb.AppendLine("      - type: gcs");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        bucket: {gcs.Bucket}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        path: {CombinePrefix(gcs.PathPrefix, fileName)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        credentials-path: {gcs.CredentialsJsonPath}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        retention: {retention}");
    }

    private static void AppendSftpReplica(StringBuilder sb, LitestreamSftpOptions sftp, string fileName, string retention)
    {
        sb.AppendLine("      - type: sftp");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        host: {sftp.Host}:{sftp.Port.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        user: {sftp.User}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        path: {CombinePrefix(sftp.Path, fileName)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        key-path: {sftp.PrivateKeyPath}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        retention: {retention}");
    }

    private static void AppendLocalDiskReplica(StringBuilder sb, LitestreamLocalDiskOptions local, string fileName, string retention)
    {
        sb.AppendLine("      - type: file");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        path: {Path.Combine(local.Path, fileName)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        retention: {retention}");
    }

    private static string CombinePrefix(string prefix, string fileName)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return fileName;
        }
        return prefix.EndsWith('/') ? prefix + fileName : prefix + "/" + fileName;
    }
}
