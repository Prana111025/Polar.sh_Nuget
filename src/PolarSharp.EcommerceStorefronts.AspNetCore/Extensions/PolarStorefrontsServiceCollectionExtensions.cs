using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStorefronts.AspNetCore.Diagnostics;
using PolarSharp.EcommerceStorefronts.Extensions;
using PolarSharp.EcommerceStorefronts.GuestSessions.Extensions;

namespace PolarSharp.EcommerceStorefronts.AspNetCore.Extensions;

/// <summary>
/// Composition root for the storefront feature. Wires storefront-core services +
/// guest-session services in one call so hosts only have one line to add to
/// <c>Program.cs</c>.
/// </summary>
public static class PolarStorefrontsServiceCollectionExtensions
{
    /// <summary>Registers every storefront-feature service against the host's DI container.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configure">Optional callback for tuning <see cref="StorefrontOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// As of v1.4.0 Phase 25 the storefront-core cart, checkout, customer, and
    /// guest-session services ship real implementations. Remaining scaffolds:
    /// the 17 order-processing / subscription-billing / refund-processing pipeline
    /// stages (Phase 26), the default <c>NullStorefrontCustomerSource</c>, and the
    /// in-process default stores. <see cref="StorefrontScaffoldDiagnosticService"/>
    /// emits a <see cref="Microsoft.Extensions.Logging.LogLevel.Warning"/> on host
    /// startup naming each remaining piece so an operator can audit before going to
    /// production.
    /// </para>
    /// <para>
    /// This is the ONE-LINE composition hosts call. Bridge packages
    /// (<c>PolarSharp.EcommerceStorefronts.Polar.Catalog</c>, <c>...Polar.Identity</c>,
    /// shipping providers, tax providers, etc.) layer their own <c>AddPolar*</c> calls
    /// on top to register concrete provider implementations.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPolarStorefronts(
        this IServiceCollection services,
        Action<StorefrontOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddPolarStorefrontsCore(configure);
        services.AddPolarGuestSessions();

        // Warning-level scaffold diagnostic — fires once on host startup naming the
        // remaining Phase 26 pipeline stages + the in-process default stores + the
        // Null customer-source default so operators can audit before going to
        // production. Removed when those pieces ship real implementations.
        services.AddHostedService<StorefrontScaffoldDiagnosticService>();

        return services;
    }
}
