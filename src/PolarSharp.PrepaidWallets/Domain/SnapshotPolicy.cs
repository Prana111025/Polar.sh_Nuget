namespace PolarSharp.PrepaidWallets.Domain;

/// <summary>
/// Decides whether a wallet's current version warrants writing a fresh snapshot. Per Case Study 02
/// step 5 ("Snapshot strategy"), the default is one snapshot every 50 events; Cosmos-backed stores
/// tighten that to every 10 events because full replay is RU-expensive.
/// </summary>
public interface ISnapshotPolicy
{
    /// <summary>
    /// True iff a snapshot should be persisted now (typically called after every successful
    /// command append).
    /// </summary>
    /// <param name="currentVersion">The wallet's version after the most recent append.</param>
    /// <param name="latestSnapshotVersion">The version of the most recent snapshot, or 0 when none exists.</param>
    bool ShouldSnapshot(long currentVersion, long latestSnapshotVersion);
}

/// <summary>
/// Stride-based snapshot policy — snapshots every N events. The default is N = 50 events; pass
/// a different stride to tune (e.g. 10 for Cosmos).
/// </summary>
public sealed class StrideSnapshotPolicy : ISnapshotPolicy
{
    /// <summary>The default stride (50 events between snapshots).</summary>
    public const int DefaultStride = 50;

    /// <summary>Construct a stride policy.</summary>
    /// <param name="stride">Events between snapshots. Must be at least 1.</param>
    public StrideSnapshotPolicy(int stride = DefaultStride)
    {
        if (stride < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "Stride must be at least 1.");
        }

        Stride = stride;
    }

    /// <summary>The configured stride.</summary>
    public int Stride { get; }

    /// <inheritdoc/>
    public bool ShouldSnapshot(long currentVersion, long latestSnapshotVersion) =>
        currentVersion > 0
        && currentVersion - latestSnapshotVersion >= Stride;
}
