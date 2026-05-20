using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Customers;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;
using PolarSharp.EcommerceStorefronts.Cart;
using PolarSharp.EcommerceStorefronts.Checkout;
using PolarSharp.EcommerceStorefronts.Customers;
using PolarSharp.EcommerceStorefronts.Identity;
using PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing;

namespace PolarSharp.EcommerceStorefronts.Extensions;

/// <summary>
/// Registration extensions for the storefront-core services.
/// </summary>
/// <remarks>
/// <see cref="AddPolarStorefrontsCore"/> registers:
/// <list type="bullet">
/// <item>The cart, checkout, and customer service implementations.</item>
/// <item>An in-memory cart store + checkout session store (suitable for development;
/// production hosts swap these for EF Core / Redis-backed replacements via
/// <c>services.AddSingleton&lt;IStorefrontCartStore, MyStore&gt;()</c> BEFORE the
/// <c>AddPolarStorefrontsCore</c> call).</item>
/// <item>An anonymous single-tenant identity provider + a Null guest-session accessor +
/// a Null customer source. Real hosts replace each of these by registering their own
/// implementation against the abstraction before calling this method;
/// <c>TryAddScoped</c> respects the prior registration.</item>
/// <item><see cref="IStorefrontClient"/> as a scoped facade over the four services.</item>
/// </list>
/// <para>
/// Catalog, search, shipping, tax, and wallet providers are intentionally NOT
/// registered here — those are wired by bridge packages
/// (<c>PolarSharp.EcommerceStorefronts.Polar.Catalog</c> and friends). Hosts must
/// register an <c>IStorefrontCatalogProvider</c> before the cart service is
/// resolved or cart mutations will fail with a DI activation error.
/// </para>
/// </remarks>
public static class StorefrontServiceCollectionExtensions
{
    /// <summary>Registers the storefront-core services + options + in-memory store defaults.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configure">Optional callback for tuning <see cref="StorefrontOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPolarStorefrontsCore(
        this IServiceCollection services,
        Action<StorefrontOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<StorefrontOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);

        // Identity + guest session defaults — hosts override by registering first.
        services.TryAddScoped<IStorefrontIdentityProvider, AnonymousSingleTenantIdentityProvider>();
        services.TryAddScoped<IGuestSessionAccessor, NullGuestSessionAccessor>();

        // Stores — singleton so carts + sessions persist across requests within a process.
        services.TryAddSingleton<IStorefrontCartStore, InMemoryStorefrontCartStore>();
        services.TryAddSingleton<IStorefrontCheckoutSessionStore, InMemoryStorefrontCheckoutSessionStore>();

        // Customer source — Null default; hosts plug a real source via the
        // PolarSharp.EcommerceStorefronts.Polar.Reporting bridge or a host-specific impl.
        services.TryAddScoped<IStorefrontCustomerSource, NullStorefrontCustomerSource>();

        services.TryAddScoped<IStorefrontCartService, DefaultStorefrontCartService>();

        // Checkout depends on an OPTIONAL OrderProcessingPipeline; use a factory so the
        // constructor's default-null parameter is honoured when the pipeline package has
        // not been registered.
        services.TryAddScoped<IStorefrontCheckoutService>(sp =>
            new DefaultStorefrontCheckoutService(
                sp.GetRequiredService<IStorefrontCartStore>(),
                sp.GetRequiredService<IStorefrontCheckoutSessionStore>(),
                sp.GetRequiredService<IStorefrontIdentityProvider>(),
                sp.GetRequiredService<IGuestSessionAccessor>(),
                sp.GetService<OrderProcessingPipeline>(),
                sp.GetRequiredService<TimeProvider>()));

        services.TryAddScoped<IStorefrontCustomerService, DefaultStorefrontCustomerService>();
        services.TryAddScoped<IStorefrontClient, StorefrontClient>();

        return services;
    }
}
