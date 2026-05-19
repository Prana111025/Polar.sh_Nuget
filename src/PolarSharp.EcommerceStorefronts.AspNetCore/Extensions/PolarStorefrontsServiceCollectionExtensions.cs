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
    /// <strong>SCAFFOLD WARNING (pre-v1.4.0):</strong> the registered cart / customer /
    /// checkout / guest-session services throw <see cref="NotImplementedException"/> on
    /// first call (Phase 25.x) and the 17 pipeline stages no-op pass-through (Phase 26.x).
    /// AddPolarStorefronts also registers
    /// <see cref="StorefrontScaffoldDiagnosticService"/>, a hosted service that emits a
    /// <see cref="Microsoft.Extensions.Logging.LogLevel.Critical"/> log on host startup
    /// naming every scaffold. DO NOT ship to production while the diagnostic is firing.
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

        // Critical-level scaffold diagnostic — fires once on host startup. Removed when
        // every Phase 25.x / 26.x scaffold has been replaced with a real implementation.
        services.AddHostedService<StorefrontScaffoldDiagnosticService>();

        return services;
    }
}
