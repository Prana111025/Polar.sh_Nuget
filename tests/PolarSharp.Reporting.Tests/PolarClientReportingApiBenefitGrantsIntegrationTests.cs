using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1.5 BenefitGrants snapshot wiring.
/// Gated on <c>POLAR_SANDBOX_TOKEN</c>; no-op when absent.
/// </summary>
/// <remarks>
/// Goes user-code → PolarClientReportingApi.FetchBenefitGrantsSinceAsync → live
/// PolarClient → <c>https://sandbox-api.polar.sh/v1/benefit-grants/</c>. Polar's
/// org-level <c>BenefitGrant</c> exposes union-wrapped GrantedAt/RevokedAt/OrderId.
/// BenefitName/BenefitKind aren't denormalized on the grant row — the snapshot surfaces
/// BenefitId as the placeholder until v2.x enriches via a join.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiBenefitGrantsIntegrationTests
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
    public async Task FetchBenefitGrants_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchBenefitGrantsSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchBenefitGrantsSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                Assert.All(rows, g =>
                {
                    Assert.False(string.IsNullOrEmpty(g.Id), "BenefitGrant.Id must be non-empty for upsert keying.");
                    Assert.False(string.IsNullOrEmpty(g.CustomerId), "BenefitGrant.CustomerId is required.");
                    Assert.False(string.IsNullOrEmpty(g.BenefitId), "BenefitGrant.BenefitId is required.");
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchBenefitGrants_with_unknown_sinceId_returns_full_page_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchBenefitGrantsSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
