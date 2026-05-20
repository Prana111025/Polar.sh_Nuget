using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1H Discounts snapshot wiring.
/// Discount has a 4-way discriminator (DiscountFixedOnceForeverDuration /
/// DiscountFixedRepeatDuration / DiscountPercentageOnceForeverDuration /
/// DiscountPercentageRepeatDuration). The wrapper probes each variant and surfaces
/// shared + variant-specific fields. Type discriminator becomes "fixed" or "percentage".
/// </summary>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiDiscountsIntegrationTests
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
    public async Task FetchDiscounts_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchDiscountsSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchDiscountsSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                Assert.All(rows, d =>
                {
                    Assert.False(string.IsNullOrEmpty(d.Id), "Discount.Id must be non-empty.");
                    Assert.False(string.IsNullOrEmpty(d.Name), "Discount.Name must be non-empty.");
                    Assert.Contains(d.Type, new[] { "fixed", "percentage" });
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchDiscounts_with_unknown_sinceId_returns_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchDiscountsSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
