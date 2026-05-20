namespace PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite.Litestream;

/// <summary>
/// Scaffolds the CLI helper commands intended to ship under <c>polar-mt litestream &lt;verb&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// The CLI surface is scaffolded in Stage C but the command bodies are deferred to
/// <c>TASK-V20-017</c> (Litestream CLI implementation). Discovery + dispatch (e.g. via
/// <c>System.CommandLine</c>) is out of scope for the Stage C deliverable; these
/// methods define the intended shape and contract so the follow-up release lands as
/// pure implementation rather than design.
/// </para>
/// <para>
/// Intended verbs:
/// </para>
/// <list type="bullet">
///   <item><c>polar-mt litestream init</c> — reads the resolved
///   <see cref="LitestreamOptions"/> + the SQLite database directory, asks
///   <see cref="LitestreamConfigGenerator"/> to produce a <c>litestream.yml</c>, and writes
///   it to the requested output path (default <c>./litestream.yml</c>).</item>
///   <item><c>polar-mt litestream verify</c> — performs a point-in-time restore of every
///   replicated <c>.db</c> file to a temporary directory, opens each restored file with
///   <c>Microsoft.Data.Sqlite</c>, runs <c>PRAGMA integrity_check</c>, and reports a
///   pass/fail summary. Useful for periodic disaster-recovery rehearsal.</item>
/// </list>
/// </remarks>
public sealed class LitestreamCliCommands
{
    private readonly LitestreamConfigGenerator _generator;

    /// <summary>Initializes a new <see cref="LitestreamCliCommands"/>.</summary>
    /// <param name="generator">The config generator used by the <c>init</c> verb.</param>
    public LitestreamCliCommands(LitestreamConfigGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
    }

    /// <summary>
    /// Implements <c>polar-mt litestream init</c>: generates a <c>litestream.yml</c> from
    /// the resolved options + database directory and writes it to <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="databaseDirectory">The SQLite database directory to scan.</param>
    /// <param name="options">The resolved Litestream options.</param>
    /// <param name="outputPath">Destination path for the generated YAML.</param>
    /// <returns>Exit code (0 on success, non-zero on failure).</returns>
    /// <exception cref="NotImplementedException">Always. Tracked as TASK-V20-017; see TASKS.md.</exception>
    public int Init(string databaseDirectory, LitestreamOptions options, string outputPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(databaseDirectory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        // Reference the generator so the field is not flagged as unused while the body is
        // a scaffold. The full implementation will invoke _generator.Generate(...) and
        // File.WriteAllText(outputPath, ...).
        _ = _generator;

        throw new NotImplementedException(
            "TASK-V20-017 deferred — Stage C scaffolds the CLI shape only. " +
            "Full discovery + dispatch (via System.CommandLine) lands in the follow-up release.");
    }

    /// <summary>
    /// Implements <c>polar-mt litestream verify</c>: restores every replicated database to
    /// a temporary directory, runs <c>PRAGMA integrity_check</c> on each, and reports.
    /// </summary>
    /// <param name="databaseDirectory">The SQLite database directory whose files have replicas.</param>
    /// <param name="options">The resolved Litestream options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 when every replica restores and verifies clean; non-zero otherwise).</returns>
    /// <exception cref="NotImplementedException">Always. Tracked as TASK-V20-017; see TASKS.md.</exception>
    public Task<int> VerifyAsync(string databaseDirectory, LitestreamOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(databaseDirectory);
        ArgumentNullException.ThrowIfNull(options);

        throw new NotImplementedException(
            "TASK-V20-017 deferred — Stage C scaffolds the CLI shape only. " +
            "Full restore-and-verify smoke test lands in the follow-up release.");
    }
}
