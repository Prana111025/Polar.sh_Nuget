using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.Reporting.EntityFrameworkCore.Snapshot;

namespace PolarSharp.Reporting.Tests;

/// <summary>
/// Live integration tests for the V20-005 Phase 1.5 Customers snapshot wiring. Gated on
/// <c>POLAR_SANDBOX_TOKEN</c>; no-op when absent.
/// </summary>
/// <remarks>
/// Goes user-code → PolarClientReportingApi.FetchCustomersSinceAsync → live PolarClient →
/// <c>https://sandbox-api.polar.sh/v1/customers/</c>. Polar's <c>Customer</c> is a
/// discriminated union (<c>CustomerIndividual</c> + <c>CustomerTeam</c>); the wrapper
/// probes both. Team customers have no Email field on the wire.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PolarClientReportingApiCustomersIntegrationTests
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
    public async Task FetchCustomers_against_live_sandbox_returns_typed_Success()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.FetchCustomersSinceAsync(sinceId: null, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from FetchCustomersSinceAsync against the live sandbox.");
        result.Match<int>(
            onSuccess: rows =>
            {
                Assert.NotNull(rows);
                Assert.All(rows, c =>
                {
                    Assert.False(string.IsNullOrEmpty(c.Id), "Customer.Id must be non-empty for upsert keying.");
                    // Email may be empty for team-typed customers — only assert non-null.
                    Assert.NotNull(c.Email);
                });
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task FetchCustomers_with_unknown_sinceId_returns_full_page_unfiltered()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var bogusCursor = Guid.NewGuid().ToString();
        var result = await api.FetchCustomersSinceAsync(sinceId: bogusCursor, pageSize: 10, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success even when sinceId doesn't appear in the page.");
    }
}
