using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp;
using PolarSharp.EcommerceStoreManagement;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;
using PolarSharp.EcommerceStoreManagement.Services;

namespace PolarSharp.EcommerceStoreManagement.Tests;

/// <summary>
/// Live integration tests for <see cref="PolarClientRefundsApi"/> (TASK-V20-002 wiring).
/// Gated on the <c>POLAR_SANDBOX_TOKEN</c> environment variable — present locally via direnv
/// and in CI via the GitHub Actions secret of the same name. When the token is absent the
/// tests no-op (return early without asserting), so unit-test passes on machines without
/// sandbox access stay green.
/// </summary>
/// <remarks>
/// Marked <c>[Trait("Category", "Integration")]</c> so the default
/// <c>dotnet test --filter "Category!=Integration"</c> from the build job ignores them; the
/// <c>integration-test</c> job in CI explicitly includes them via
/// <c>--filter Category=Integration</c>.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PolarClientRefundsApiIntegrationTests
{
    private static string? Token => Environment.GetEnvironmentVariable("POLAR_SANDBOX_TOKEN");

    /// <summary>
    /// A fresh Guid.NewGuid() — UUIDv4 format (Polar requires v4 specifically; non-v4 UUIDs
    /// or non-UUID strings get a 422 from the validator). Per-test-class instance avoids
    /// theoretical collision with real ids in the sandbox org.
    /// </summary>
    private static readonly string NonexistentOrderId = Guid.NewGuid().ToString();

    private static PolarClientRefundsApi BuildApi()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://sandbox-api.polar.sh") };
        http.DefaultRequestHeaders.Authorization = new("Bearer", Token);
        var polar = new PolarClient(http);
        return new PolarClientRefundsApi(polar, NullLogger<PolarClientRefundsApi>.Instance);
    }

    [SkippableFact]
    public async Task ListRefundsForOrder_returns_success_when_no_refunds_match()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();

        // Use a deliberately non-existent order id. Polar's listing endpoint should return an
        // empty page (NOT 404) when filtering on an unknown order id — the filter just matches
        // zero rows. Verifies the GET path through PolarClient end-to-end.
        var result = await api.ListRefundsForOrderAsync(NonexistentOrderId, CancellationToken.None);

        Assert.True(result.IsSuccess, "Expected Success from ListRefundsForOrderAsync; got Failure.");
        result.Match<int>(
            onSuccess: rows => { Assert.NotNull(rows); Assert.Empty(rows); return 0; },
            onFailure: err => throw new Xunit.Sdk.XunitException($"Unexpected failure: {err.Kind} — {err.Message}"));
    }

    [SkippableFact]
    public async Task CreateRefund_against_unknown_order_returns_typed_error_not_exception()
    {
        Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");

        var api = BuildApi();

        // Attempt to refund a non-existent order. Polar should respond 404 or 400; the wrapper
        // must return a typed RefundApiError rather than throwing — that's what makes the
        // service-level RefundService.IssueXxxRefundAsync code path safe to call.
        var request = new RefundApiRequest(
            PolarOrderId: NonexistentOrderId,
            Amount: 100,
            Currency: "USD",
            Reason: RefundReason.Other,
            Comment: "Integration test against non-existent order — expecting typed failure.");

        var result = await api.CreateRefundAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure, "Expected Failure for non-existent order; got Success.");
        result.Match<int>(
            onSuccess: _ => throw new Xunit.Sdk.XunitException("Expected Failure"),
            onFailure: err =>
            {
                // Either OrderNotFound or AmountExceedsRefundable depending on Polar's
                // validation order; both are expected. The CRITICAL invariant is that the
                // wrapper returned a typed error rather than letting the ApiException leak.
                Assert.True(
                    err.Kind is RefundApiErrorKind.OrderNotFound
                            or RefundApiErrorKind.AmountExceedsRefundable
                            or RefundApiErrorKind.UnexpectedFailure,
                    $"Got unexpected error kind {err.Kind}: {err.Message}");
                Assert.False(string.IsNullOrEmpty(err.Message), "Error message must be non-empty for diagnostics.");
                return 0;
            });
    }
}
