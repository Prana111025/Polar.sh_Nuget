using System.Xml.Linq;

namespace PolarSharp.Tests;

/// <summary>
/// Repo-structure invariant tests for the IsPackable=false scaffold packages.
/// </summary>
/// <remarks>
/// <para>
/// The repo intentionally ships ~30 src packages marked <c>&lt;IsPackable&gt;false&lt;/IsPackable&gt;</c>
/// — Storefronts Polar bridges, themes, SEO/Search/Shipping/Tax/WebComponents providers,
/// CustomerGraph + NLQ + Marten bridges, etc. These are scaffolds: csproj + README only,
/// no hand-written source, holding their place in the package tree until their owning
/// phase ships a real implementation.
/// </para>
/// <para>
/// Without this test, "scaffold by design" and "broken real package whose IsPackable flag
/// got flipped" are indistinguishable to anyone scanning the build output. This test pins
/// the invariant: every IsPackable=false package under <c>src/</c> has fewer than
/// <see cref="MaxHandWrittenFiles"/> hand-written <c>.cs</c> files. When a scaffold gets
/// real, the contributor must drop the IsPackable=false flag in the same commit, which
/// keeps this test green and intent honest.
/// </para>
/// </remarks>
public sealed class ScaffoldIntegrityTests
{
    /// <summary>
    /// Generous threshold: allows a scaffold to ship a marker type, a DI-extension stub,
    /// or one or two abstraction types without tripping the test. A package with this
    /// many or more hand-written files is genuinely doing something and shouldn't be
    /// flagged IsPackable=false anymore.
    /// </summary>
    private const int MaxHandWrittenFiles = 3;

    [Fact]
    public void Every_scaffold_package_has_minimal_hand_written_source()
    {
        var repoRoot = FindRepoRoot();
        var srcDir = Path.Combine(repoRoot, "src");
        Assert.True(Directory.Exists(srcDir), $"Expected src/ at {srcDir}");

        var scaffolds = Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories)
            .Where(IsScaffold)
            .ToList();

        Assert.True(scaffolds.Count > 0,
            "Found zero IsPackable=false scaffolds — either the repo dropped its scaffold packages or the IsPackable detection broke.");

        var failures = new List<string>();
        foreach (var csproj in scaffolds)
        {
            var pkgDir = Path.GetDirectoryName(csproj)!;
            var csFiles = Directory.GetFiles(pkgDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToList();

            if (csFiles.Count > MaxHandWrittenFiles)
            {
                failures.Add(
                    $"{Path.GetRelativePath(repoRoot, csproj)}: {csFiles.Count} hand-written .cs files (threshold {MaxHandWrittenFiles}). " +
                    "Either drop <IsPackable>false</IsPackable> if this package now has real content, or split the real content into a separate package and keep this scaffold empty.");
            }
        }

        Assert.True(failures.Count == 0,
            "One or more IsPackable=false packages exceed the scaffold size threshold:\n  - " +
            string.Join("\n  - ", failures));
    }

    [Fact]
    public void Every_scaffold_package_has_a_readme()
    {
        var repoRoot = FindRepoRoot();
        var srcDir = Path.Combine(repoRoot, "src");

        var scaffolds = Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories)
            .Where(IsScaffold)
            .ToList();

        var failures = scaffolds
            .Where(csproj => !File.Exists(Path.Combine(Path.GetDirectoryName(csproj)!, "README.md")))
            .Select(csproj => Path.GetRelativePath(repoRoot, csproj))
            .ToList();

        Assert.True(failures.Count == 0,
            "Scaffold packages are required to ship a README explaining their intended scope (per CLAUDE.md per-package README rule). Missing:\n  - " +
            string.Join("\n  - ", failures));
    }

    private static bool IsScaffold(string csprojPath)
    {
        try
        {
            var doc = XDocument.Load(csprojPath);
            return doc.Descendants("IsPackable")
                .Any(e => string.Equals(e.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PolarSharp.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException(
            "Could not locate repo root (no PolarSharp.slnx found by walking up from AppContext.BaseDirectory).");
    }
}
