using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PolarSharp.EcommerceStorefronts.AspNetCore.Diagnostics;

/// <summary>
/// IHostedService that emits a <see cref="LogLevel.Warning"/> diagnostic on host startup
/// listing the storefront-feature pieces that are still scaffold-state. The log fires
/// once at startup so a host operator who calls
/// <see cref="Extensions.PolarStorefrontsServiceCollectionExtensions.AddPolarStorefronts"/>
/// gets a clear signal about which capabilities require provider wiring before going
/// to production.
/// </summary>
/// <remarks>
/// <para>
/// As of v1.4.0 Phase 25 the storefront-core cart, checkout, customer, and guest-session
/// services ship real implementations — they no longer throw
/// <see cref="NotImplementedException"/>. What remains scaffold-state is:
/// </para>
/// <list type="bullet">
/// <item>The 17 order-processing / subscription-billing / refund-processing pipeline
/// stages (Phase 26) — they no-op pass-through with a debug log.</item>
/// <item>The default <c>IStorefrontCustomerSource</c> (<c>NullStorefrontCustomerSource</c>),
/// which returns empty / NotFound for every read until a Polar bridge supplies a real
/// source (Phase 31).</item>
/// <item>The default <c>IStorefrontCartStore</c> +
/// <c>IStorefrontCheckoutSessionStore</c> + <c>IStorefrontIdempotencyCache</c> are
/// in-process and don't survive a process restart — production multi-server hosts plug
/// EF Core / Redis-backed replacements.</item>
/// </list>
/// <para>
/// Suppress the warning by filtering the log category
/// <c>PolarSharp.EcommerceStorefronts.AspNetCore.Diagnostics.StorefrontScaffoldDiagnosticService</c>
/// in your logging configuration once you've consciously wired each piece to a
/// production-grade implementation.
/// </para>
/// </remarks>
public sealed class StorefrontScaffoldDiagnosticService : IHostedService
{
    private readonly ILogger<StorefrontScaffoldDiagnosticService> _logger;

    /// <summary>Initializes a new diagnostic service.</summary>
    /// <param name="logger">Logger used for the Warning-level diagnostic message.</param>
    public StorefrontScaffoldDiagnosticService(ILogger<StorefrontScaffoldDiagnosticService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "PolarSharp.EcommerceStorefronts Phase 25 services (cart, checkout, customer, " +
            "guest-session) are wired and functional. Remaining scaffolds: " +
            "(1) the 17 order-processing/subscription-billing/refund-processing pipeline " +
            "stages no-op pass-through (Phase 26); " +
            "(2) the default IStorefrontCustomerSource is NullStorefrontCustomerSource — " +
            "register a real source via the Polar.Reporting bridge or your own impl before " +
            "exposing customer self-service to real users; " +
            "(3) the default IStorefrontCartStore, IStorefrontCheckoutSessionStore, and " +
            "IStorefrontIdempotencyCache are in-process and won't survive a restart — swap " +
            "in EF Core / Redis-backed replacements for production multi-server deployments. " +
            "Suppress this message by filtering log category " +
            "'PolarSharp.EcommerceStorefronts.AspNetCore.Diagnostics.StorefrontScaffoldDiagnosticService' " +
            "once each piece is consciously addressed.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
