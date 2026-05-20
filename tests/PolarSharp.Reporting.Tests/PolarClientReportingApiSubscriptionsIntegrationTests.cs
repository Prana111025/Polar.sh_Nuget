using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1.5 Subscriptions snapshot wiring. Gated
/// on <c>POLAR_SANDBOX_TOKEN</c>; no-op when absent.
/// </summary>
/// <remarks>
/// Goes user-code → PolarClientReportingApi.FetchSubscriptionsSinceAsync → live
/// PolarClient → <c>https://sandbox-api.polar.sh/v1/subscriptions/</c>.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiSubscriptionsIntegrationTests
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
    public async Task FetchSubscriptions_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchSubscriptionsSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchSubscriptionsSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                Assert.All(rows, s =>
                {
                    Assert.False(string.IsNullOrEmpty(s.Id), "Subscription.Id must be non-empty for upsert keying.");
                    Assert.False(string.IsNullOrEmpty(s.CustomerId), "Subscription.CustomerId is required by ReportSubscriptionEntity.");
                    Assert.False(string.IsNullOrEmpty(s.ProductId), "Subscription.ProductId is required by ReportSubscriptionEntity.");
                    Assert.False(string.IsNullOrEmpty(s.Status), "Subscription.Status is required by ReportSubscriptionEntity.");
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchSubscriptions_with_unknown_sinceId_returns_full_page_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchSubscriptionsSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
