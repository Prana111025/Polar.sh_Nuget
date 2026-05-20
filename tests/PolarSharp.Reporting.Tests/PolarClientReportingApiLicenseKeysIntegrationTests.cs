using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1D LicenseKeys snapshot wiring.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiLicenseKeysIntegrationTests
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
    public async Task FetchLicenseKeys_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchLicenseKeysSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchLicenseKeysSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                Assert.All(rows, lk =>
                {
                    Assert.False(string.IsNullOrEmpty(lk.Id), "LicenseKey.Id must be non-empty for upsert keying.");
                    Assert.False(string.IsNullOrEmpty(lk.CustomerId), "LicenseKey.CustomerId is required.");
                    Assert.False(string.IsNullOrEmpty(lk.Status), "LicenseKey.Status must be set.");
                    // We intentionally do NOT assert on the raw key — the snapshot never carries
                    // the raw value; only the masked DisplayKey is surfaced.
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchLicenseKeys_with_unknown_sinceId_returns_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchLicenseKeysSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
