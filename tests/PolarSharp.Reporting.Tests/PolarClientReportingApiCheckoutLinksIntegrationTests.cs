using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1G CheckoutLinks snapshot wiring.
/// CheckoutLink has many union-wrapped fields (Label, SuccessUrl, ModifiedAt, DiscountId,
/// ReturnUrl); tests exercise the multi-union extraction pattern at scale.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiCheckoutLinksIntegrationTests
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
    public async Task FetchCheckoutLinks_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchCheckoutLinksSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchCheckoutLinksSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                Assert.All(rows, cl =>
                {
                    Assert.False(string.IsNullOrEmpty(cl.Id), "CheckoutLink.Id must be non-empty.");
                    Assert.False(string.IsNullOrEmpty(cl.Label),
                        "CheckoutLink.Label must be non-empty — the wrapper should default to a placeholder rather than null.");
                    Assert.NotNull(cl.ProductIds);     // empty list OK; null is not
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchCheckoutLinks_with_unknown_sinceId_returns_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchCheckoutLinksSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
