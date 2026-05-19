using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PolarSharp.EcommerceStorefronts.AspNetCore.Diagnostics;

/// <summary>
/// IHostedService that emits a <see cref="LogLevel.Critical"/> diagnostic on host startup
/// listing every storefront service + pipeline stage that is currently a scaffold. The
/// log fires once at startup so a host operator who calls
/// <see cref="Extensions.PolarStorefrontsServiceCollectionExtensions.AddPolarStorefronts"/>
/// gets a clear, unmissable signal that the registered service tree is NOT production-ready
/// — pre-Phase 25.x / 26.x the cart, checkout, customer, guest-session, and pipeline
/// implementations either throw <see cref="NotImplementedException"/> or no-op pass-through.
/// </summary>
/// <remarks>
/// <para>
/// The diagnostic is registered automatically by <c>AddPolarStorefronts</c>. Hosts that
/// know what they're doing and don't want the warning (e.g. running scaffold demos in CI)
/// can suppress by registering their own <c>IHostedService</c> implementation with the
/// same type BEFORE calling <c>AddPolarStorefronts</c> — the <c>TryAddHostedService</c>
/// semantics would skip the default, OR more pragmatically the host can filter the log
/// category <c>PolarSharp.EcommerceStorefronts.AspNetCore.Diagnostics.StorefrontScaffoldDiagnosticService</c>
/// out of their logging configuration.
/// </para>
/// <para>
/// Removed when v1.4.0 ships real implementations: when every line in the diagnostic list
/// is no longer accurate, this hosted service is deleted and the registration call in
/// <c>AddPolarStorefronts</c> is removed.
/// </para>
/// </remarks>
public sealed class StorefrontScaffoldDiagnosticService : IHostedService
{
    private readonly ILogger<StorefrontScaffoldDiagnosticService> _logger;

    /// <summary>Initializes a new diagnostic service.</summary>
    /// <param name="logger">Logger used for the Critical-level diagnostic message.</param>
    public StorefrontScaffoldDiagnosticService(ILogger<StorefrontScaffoldDiagnosticService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogCritical(
            "PolarSharp.EcommerceStorefronts is SCAFFOLD-ONLY pre-v1.4.0. AddPolarStorefronts has " +
            "registered service skeletons that will throw NotImplementedException on first call " +
            "(IStorefrontCartService: 8 methods; IStorefrontCustomerService: 7; IStorefrontCheckoutService: 2; " +
            "IGuestSessionService: 3) and 17 pipeline stages that no-op pass-through with a debug log " +
            "(OrderProcessing: ValidateLineItems, CheckInventory, ApplyDiscounts, QuoteTax, QuoteShipping, " +
            "CapturePayment, Fulfill, Notify; SubscriptionBilling: ValidateSubscription, ApplyProration, " +
            "CheckPaymentMethod, CapturePayment, Notify; RefundProcessing: ValidateRefundEligibility, " +
            "ComputeRefundAmount, ExecuteRefund, Notify). DO NOT ship to production. " +
            "Track implementation status against Phase 25.x (services) + Phase 26.x (pipeline stages). " +
            "Suppress this message by filtering the log category " +
            "'PolarSharp.EcommerceStorefronts.AspNetCore.Diagnostics.StorefrontScaffoldDiagnosticService' " +
            "in your logging configuration.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
