using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1B Products snapshot wiring. Gated on
/// <c>POLAR_SANDBOX_TOKEN</c> (loaded via direnv from <c>.env</c> locally; from the GitHub
/// Actions secret in CI). When absent, tests no-op so unit-test runs on machines without
/// sandbox access stay green.
/// </summary>
/// <remarks>
/// Goes user-code → PolarClientReportingApi.FetchProductsSinceAsync → live PolarClient →
/// <c>https://sandbox-api.polar.sh/v1/products/</c>. No raw HttpClient in the test — the
/// wrapper IS the system under test (per the live-Polar-tests-required project rule).
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiProductsIntegrationTests
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
    public async Task FetchProducts_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchProductsSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchProductsSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                // The sandbox may have zero or more products depending on operator state — we
                // assert structural shape only. Every row that comes back must have a non-empty
                // Id (the snapshot service uses it as the upsert key) and a non-empty Name
                // (NOT NULL in the entity config). This catches any future Kiota regeneration
                // that drops these fields or changes their nullability.
                Assert.All(rows, p =>
                {
                    Assert.False(string.IsNullOrEmpty(p.Id), "Product.Id must be non-empty for upsert keying.");
                    Assert.False(string.IsNullOrEmpty(p.Name), "Product.Name is required by ReportProductEntity.");
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchProducts_with_unknown_sinceId_returns_full_page_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        // A non-existent (but well-formed) sinceId. The wrapper should still return all rows
        // (since the cursor doesn't appear in the page) rather than failing.
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchProductsSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
