using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1F Meters snapshot wiring.
/// Meter.Aggregation is a discriminated union (CountAggregation / PropertyAggregation /
/// UniqueAggregation); the wrapper extracts the populated variant's Func and surfaces it
/// as the AggregationKind string.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiMetersIntegrationTests
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
    public async Task FetchMeters_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchMetersSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchMetersSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                Assert.All(rows, m =>
                {
                    Assert.False(string.IsNullOrEmpty(m.Id), "Meter.Id must be non-empty.");
                    Assert.False(string.IsNullOrEmpty(m.Name), "Meter.Name must be non-empty.");
                    Assert.False(string.IsNullOrEmpty(m.AggregationKind),
                        "Meter.AggregationKind must be non-empty — the wrapper should default to 'unknown' rather than null.");
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchMeters_with_unknown_sinceId_returns_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchMetersSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
