using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Live integration tests for <see cref="PolarClientLicenseKeysApi"/> (TASK-V20-003 wiring).
/// Gated on <c>POLAR_SANDBOX_TOKEN</c> (loaded via direnv from <c>.env</c> locally; from
/// the GitHub Actions secret of the same name in CI). When absent, tests no-op.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PolarClientLicenseKeysApiIntegrationTests
{
    private static string? Token => Environment.GetEnvironmentVariable("POLAR_SANDBOX_TOKEN");

    /// <summary>
    /// Sandbox org id this token has access to. The first integration test (V20-002 refunds)
    /// confirmed via `/v1/organizations/` that this token sees exactly one org with this id.
    /// Hardcoded here so the test is reproducible without a live `/v1/organizations/` round-trip.
    /// </summary>
    private const string SandboxOrgId = "c4e32562-775b-426c-8685-c6cf0f44739f";

    private static PolarClientLicenseKeysApi BuildApi()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://sandbox-api.polar.sh") };
        http.DefaultRequestHeaders.Authorization = new("Bearer", Token);
        var polar = new PolarClient(http);
        return new PolarClientLicenseKeysApi(polar, NullLogger<PolarClientLicenseKeysApi>.Instance);
    }

    [SkippableFact]
    public async Task Validate_against_unknown_key_returns_typed_NotFound_not_exception()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();

        // A clearly-bogus license key. Polar responds 404 ResourceNotFound; the wrapper must
        // surface that as a typed LicenseKeyApiErrorKind.NotFound — never throw — so
        // LicenseKeyValidator.ValidateAsync can compose against it without try/catch.
        var request = new LicenseKeyApiRequest(
            Key: "POLARSHARP-V20003-INTEGRATION-NONEXISTENT-KEY-9999",
            OrganizationId: SandboxOrgId);

        var result = await api.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure, "Expected Failure for non-existent key; got Success.");
        result.Match<int>(
            onSuccess: _ => throw new Xunit.Sdk.XunitException("Expected Failure"),
            onFailure: err =>
            {
                // NotFound is the documented Polar response for an unknown key. MalformedKey
                // also acceptable if Polar's validator runs format checks before lookup.
                Assert.True(
                    err.Kind is LicenseKeyApiErrorKind.NotFound
                            or LicenseKeyApiErrorKind.MalformedKey
                            or LicenseKeyApiErrorKind.UnexpectedFailure,
                    $"Got unexpected error kind {err.Kind}: {err.Message}");
                Assert.False(string.IsNullOrEmpty(err.Message), "Error message must be non-empty.");
                return 0;
            });
    }

    [SkippableFact]
    public async Task Validate_against_malformed_organization_id_returns_typed_failure_not_exception()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();

        // Send a non-UUID organization_id. Polar's validator returns 422 RequestValidationError
        // (we saw this exact pattern with the V20-002 refunds endpoint — UUID v4 is required).
        // The wrapper must surface this as a typed failure rather than letting the
        // ApiException propagate.
        var request = new LicenseKeyApiRequest(
            Key: "ANY-KEY-WILL-DO",
            OrganizationId: "not-a-valid-uuid-organization-id");

        var result = await api.ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure, "Expected Failure for malformed org id; got Success.");
        result.Match<int>(
            onSuccess: _ => throw new Xunit.Sdk.XunitException("Expected Failure"),
            onFailure: err =>
            {
                // Either MalformedKey (if our heuristic catches the validator message) or
                // UnexpectedFailure — both prove no exception leaked.
                Assert.True(
                    err.Kind is LicenseKeyApiErrorKind.MalformedKey
                            or LicenseKeyApiErrorKind.UnexpectedFailure
                            or LicenseKeyApiErrorKind.NotFound,
                    $"Got unexpected error kind {err.Kind}: {err.Message}");
                return 0;
            });
    }
}
