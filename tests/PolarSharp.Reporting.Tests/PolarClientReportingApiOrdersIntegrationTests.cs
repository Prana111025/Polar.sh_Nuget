using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1.5 Orders snapshot wiring. Gated on
/// <c>POLAR_SANDBOX_TOKEN</c>; no-op when absent.
/// </summary>
/// <remarks>
/// Goes user-code → PolarClientReportingApi.FetchOrdersSinceAsync → live PolarClient →
/// <c>https://sandbox-api.polar.sh/v1/orders/</c>. Asserts top-level fields are present;
/// LineItems and Refunds are intentionally empty in this phase (Polar doesn't surface
/// ProductId on OrderItemSchema and has no nested Refunds list — both fields are
/// deferred to a v2.x enrichment).
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiOrdersIntegrationTests
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
    public async Task FetchOrders_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchOrdersSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchOrdersSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                Assert.All(rows, o =>
                {
                    Assert.False(string.IsNullOrEmpty(o.Id), "Order.Id must be non-empty for upsert keying.");
                    Assert.False(string.IsNullOrEmpty(o.CustomerId), "Order.CustomerId is required by ReportOrderEntity.");
                    Assert.False(string.IsNullOrEmpty(o.Status), "Order.Status is required by ReportOrderEntity.");
                    // Phase 1.5 deliberately omits per-line-item + per-refund detail.
                    Assert.Empty(o.LineItems);
                    Assert.Empty(o.Refunds);
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchOrders_with_unknown_sinceId_returns_full_page_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchOrdersSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
