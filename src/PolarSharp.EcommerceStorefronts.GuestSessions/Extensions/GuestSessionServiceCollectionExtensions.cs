using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;

namespace PolarSharp.EcommerceStorefronts.GuestSessions.Extensions;

/// <summary>
/// Registration extensions for guest-session services.
/// </summary>
/// <remarks>
/// Hosts normally do not call this directly — it is wired by
/// <c>AddPolarStorefronts()</c> on the AspNetCore composition package. Direct calls
/// are useful for tests + for hosts that want guest sessions without the rest of the
/// storefront stack.
/// </remarks>
public static class GuestSessionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default <see cref="IGuestSessionService"/>, the
    /// <see cref="HttpContextGuestSessionAccessor"/> as <see cref="IGuestSessionAccessor"/>,
    /// the <c>IHttpContextAccessor</c>, and the data-protection wiring required for
    /// signed cookies.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPolarGuestSessions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // ASP.NET Core's AddDataProtection is idempotent — registering it here means hosts
        // that have not configured data protection explicitly still get a working
        // signed-cookie pipeline.
        services.AddDataProtection();
        services.AddHttpContextAccessor();

        services.TryAddScoped<IGuestSessionService, SignedCookieGuestSessionService>();

        // Replace any prior IGuestSessionAccessor registration with the HTTP-backed one
        // so the storefront-core NullGuestSessionAccessor default is overridden when
        // the host opts into guest sessions.
        services.Replace(ServiceDescriptor.Scoped<IGuestSessionAccessor, HttpContextGuestSessionAccessor>());

        return services;
    }
}
