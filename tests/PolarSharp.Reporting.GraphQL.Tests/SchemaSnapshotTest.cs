using HotChocolate;
using VerifyXunit;

namespace PolarSharp.Reporting.GraphQL.Tests;

/// <summary>
/// CI gate against accidental GraphQL schema drift. Prints the PolarReporting schema SDL and
/// asserts it matches the committed snapshot. Promote a <c>.received.txt</c> to
/// <c>.verified.txt</c> after a deliberate schema change.
/// </summary>
public class SchemaSnapshotTest
{
    [Fact]
    public async Task PolarReportingSchema_HasNotChanged()
    {
        var (executor, _) = await TestExecutorFactory.CreateAsync().ConfigureAwait(true);
        var sdl = executor.Schema.ToString();

        await Verifier.Verify(sdl, extension: "graphql")
            .UseDirectory("Snapshots")
            .UseFileName("PolarReportingSchema")
            .ConfigureAwait(true);
    }
}
