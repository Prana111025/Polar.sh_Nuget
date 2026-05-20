using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1C CustomerMeters snapshot wiring.
/// Gated on <c>POLAR_SANDBOX_TOKEN</c>; tests no-op when absent.
/// </summary>
/// <remarks>
/// Goes user-code → PolarClientReportingApi.FetchCustomerMetersSinceAsync → live PolarClient →
/// <c>https://sandbox-api.polar.sh/v1/customer-meters/</c>. No raw HttpClient in the test.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiCustomerMetersIntegrationTests
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
    public async Task FetchCustomerMeters_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchCustomerMetersSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchCustomerMetersSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                Assert.All(rows, cm =>
                {
                    Assert.False(string.IsNullOrEmpty(cm.Id), "CustomerMeter.Id must be non-empty for upsert keying.");
                    Assert.False(string.IsNullOrEmpty(cm.CustomerId), "CustomerMeter.CustomerId is required by ReportCustomerMeterEntity.");
                    Assert.False(string.IsNullOrEmpty(cm.MeterId), "CustomerMeter.MeterId is required by ReportCustomerMeterEntity.");
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchCustomerMeters_with_unknown_sinceId_returns_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchCustomerMetersSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
