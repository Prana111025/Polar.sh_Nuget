using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1.5 Events snapshot wiring. Gated on
/// <c>POLAR_SANDBOX_TOKEN</c>; no-op when absent.
/// </summary>
/// <remarks>
/// Goes user-code → PolarClientReportingApi.FetchEventsSinceAsync → live PolarClient →
/// <c>https://sandbox-api.polar.sh/v1/events/</c>. Events uses <c>GetAsGetResponseAsync</c>
/// (the older <c>GetAsync</c> is obsolete on this resource); the response is itself a
/// discriminated union of two list-shapes — both expose <c>.Items</c> and the wrapper
/// extracts from whichever is populated.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiEventsIntegrationTests
{
    private static string? Token => Environment.GetEnvironmentVariable("POLAR_SANDBOX_TOKEN");

    private static PolarClientReportingApi BuildApi()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://sandbox-api.polar.sh") };
        http.DefaultRequestHeaders.Authorization = new("Bearer", Token);
        var polar = new PolarClient(http);
        return new PolarClientReportingApi(polar, NullLogger<PolarClientReportingApi>.Instance);
    }

    [SkippableFact]
    public async Task FetchEvents_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchEventsSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchEventsSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                // Sandbox may have zero or many events; assert structural shape only.
                // Each row must have a non-empty Id (snapshot upsert key) and a non-empty
                // Type ("system:..." or "user:..." per the wrapper's discriminator probing).
                Assert.All(rows, e =>
                {
                    Assert.False(string.IsNullOrEmpty(e.Id), "Event.Id must be non-empty for upsert keying.");
                    Assert.False(string.IsNullOrEmpty(e.Type), "Event.Type is required by ReportEventEntity.");
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchEvents_with_unknown_sinceId_returns_full_page_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchEventsSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
