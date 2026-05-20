using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.EcommerceStoreManagement;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Live integration tests for <see cref="PolarClientOrganizationsApi"/> (TASK-V20-004
/// wiring). Exercises both <c>GET</c> and <c>PATCH</c> /v1/organizations/{id}. The PATCH
/// path uses a deliberately non-existent org id so the live test can prove typed-error
/// handling WITHOUT mutating the real sandbox org's settings — important because this
/// integration runs in CI on every push and we don't want it churning org config as a
/// side effect.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PolarClientOrganizationsApiIntegrationTests
{
    private static string? Token => Environment.GetEnvironmentVariable("POLAR_SANDBOX_TOKEN");

    /// <summary>The real sandbox org this token has access to (verified earlier via /v1/organizations/).</summary>
    private const string SandboxOrgId = "c4e32562-775b-426c-8685-c6cf0f44739f";

    private static PolarClientOrganizationsApi BuildApi()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://sandbox-api.polar.sh") };
        http.DefaultRequestHeaders.Authorization = new("Bearer", Token);
        var polar = new PolarClient(http);
        return new PolarClientOrganizationsApi(polar, NullLogger<PolarClientOrganizationsApi>.Instance);
    }

    [SkippableFact]
    public async Task GetAsync_against_real_sandbox_org_returns_id_and_account_fields()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        var result = await api.GetAsync(SandboxOrgId, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from GetAsync against real sandbox org.");
        result.Match<int>(
            onSuccess: org =>
            {
                Assert.Equal(SandboxOrgId, org.Id);
                // The real sandbox org has DefaultPresentmentCurrency="usd" and an
                // account_id set (Stripe-Connect handoff started). PayoutAccountId may be
                // null until the merchant finishes the dashboard flow — that's the whole
                // point of RefreshPayoutStatusAsync polling for it. We only assert
                // structural shape, not specific values, so the test stays robust to
                // operator changes in the sandbox dashboard.
                Assert.False(string.IsNullOrEmpty(org.DefaultPresentmentCurrency),
                    "Expected DefaultPresentmentCurrency to be set on a real org.");
                return 0;
            },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task GetAsync_against_unknown_org_id_returns_typed_NotFound_not_exception()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        // Fresh v4 UUID — not the real org id, not malformed (avoids 422), just non-existent (404).
        var nonExistentOrgId = Guid.NewGuid().ToString();

        var result = await api.GetAsync(nonExistentOrgId, CancellationToken.None);

        Assert.True(result.IsFailure, "Expected Failure for non-existent org id.");
        result.Match<int>(
            onSuccess: _ => throw new Xunit.Sdk.XunitException("Expected Failure"),
            onFailure: err =>
            {
                Assert.True(
                    err.Kind is OrganizationApiErrorKind.NotFound
                            or OrganizationApiErrorKind.UnexpectedFailure,
                    $"Got unexpected error kind {err.Kind}: {err.Message}");
                Assert.False(string.IsNullOrEmpty(err.Message), "Error message must be non-empty.");
                return 0;
            });
    }

    [SkippableFact]
    public async Task UpdateAsync_against_unknown_org_id_returns_typed_failure_not_exception()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();
        // Deliberately PATCH a non-existent org so the live test never mutates the real
        // sandbox org's settings. Polar should respond 404 (NotFound) or 403 (auth scope
        // mismatch on a foreign org). The wrapper must surface either as a typed error.
        var nonExistentOrgId = Guid.NewGuid().ToString();
        var request = new OrganizationUpdateRequest(
            Country: null,
            DefaultPresentmentCurrency: null,
            TaxBehavior: DefaultTaxBehavior.Location,
            ProductDescription: null,
            IntendedUse: null,
            PricingModels: [],
            SellingCategories: [],
            FutureAnnualRevenue: null,
            SwitchingFrom: null);

        var result = await api.UpdateAsync(nonExistentOrgId, request, CancellationToken.None);

        Assert.True(result.IsFailure, "Expected Failure for PATCH on non-existent org.");
        result.Match<int>(
            onSuccess: _ => throw new Xunit.Sdk.XunitException("Expected Failure"),
            onFailure: err =>
            {
                // NotFound / ValidationFailed / UnexpectedFailure all acceptable — the
                // CRITICAL invariant is that no exception leaks. A 403 (foreign-org auth
                // mismatch) maps to UnexpectedFailure today; that's fine for v1 of the
                // typed-error mapping (a v2.0 follow-up could add an Unauthorized variant).
                Assert.True(
                    err.Kind is OrganizationApiErrorKind.NotFound
                            or OrganizationApiErrorKind.ValidationFailed
                            or OrganizationApiErrorKind.UnexpectedFailure,
                    $"Got unexpected error kind {err.Kind}: {err.Message}");
                return 0;
            });
    }
}
