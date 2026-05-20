using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1E Benefits snapshot wiring.
/// Benefits is the first discriminated-union resource — Polar's <c>Benefit</c> wrapper has
/// 7 mutually-exclusive subtype properties; tests verify that the wrapper extracts whichever
/// variant is populated and surfaces the Kind discriminator correctly.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiBenefitsIntegrationTests
{
    private static string? Token => Environment.GetEnvironmentVariable("POLAR_SANDBOX_TOKEN");

    private static PolarClientReportingApi BuildApi()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://sandbox-api.polar.sh") };
        http.DefaultRequestHeaders.Authorization = new("Bearer", Token);
        var polar = new PolarClient(http);
        return new PolarClientReportingApi(polar, NullLogger<PolarClientReportingApi>.Instance);
    }

    private static readonly string[] KnownBenefitKinds =
    [
        "custom", "discord", "downloadables", "feature_flag",
        "github_repository", "license_keys", "meter_credit",
    ];

    [SkippableFact]
    public async Task FetchBenefits_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchBenefitsSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchBenefitsSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                Assert.All(rows, b =>
                {
                    Assert.False(string.IsNullOrEmpty(b.Id), "Benefit.Id must be non-empty.");
                    Assert.False(string.IsNullOrEmpty(b.Name), "Benefit.Name must be non-empty.");
                    Assert.Contains(b.Kind, KnownBenefitKinds);  // discriminator must map to a known wire value
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchBenefits_with_unknown_sinceId_returns_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchBenefitsSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
