using System.Threading.Channels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Publishing;
using PolarSharp.EcommerceStoreManagement.Publishing;
using PolarSharp.EcommerceStoreManagement.Services;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Services;

/// <summary>
/// DI registration for the v1.3.C catalog services (<see cref="IRefundService"/> +
/// <see cref="ILicenseKeyValidator"/>) plus their default Polar HTTP wrappers.
/// </summary>
/// <remarks>
/// <para>
/// Hosts wanting different Polar HTTP behaviour (e.g. for sandbox testing, custom retry
/// policies, or to short-circuit the deferred TASK-V20-002 / TASK-V20-003 implementations)
/// can register their own <see cref="IPolarRefundsApi"/> / <see cref="IPolarLicenseKeysApi"/>
/// before calling this method — the <c>TryAdd</c> registrations below leave their wiring
/// in place.
/// </para>
/// <para>
/// This extension will be composed into the future <c>AddPolarEcommerce()</c> orchestrator
/// (v1.3.G) along with the translation services and other catalog implementations.
/// </para>
/// </remarks>
public static class CatalogServicesExtensions
{
    /// <summary>
    /// Registers <see cref="IRefundService"/>, <see cref="ILicenseKeyValidator"/>, their
    /// default Polar HTTP wrappers, and the shared infrastructure (memory cache, audit-log
    /// actor provider, <see cref="LicenseValidatorOptions"/> binding).
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">Application configuration providing the LicenseValidator options.</param>
    public static IServiceCollection AddPolarCatalogServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<LicenseValidatorOptions>(
            configuration.GetSection(LicenseValidatorOptions.SectionName));
        services.Configure<BusinessProfileOptions>(
            configuration.GetSection(BusinessProfileOptions.SectionName));

        services.AddMemoryCache();
        services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);
        services.TryAddScoped<IAuditLogActorProvider, SystemAuditLogActorProvider>();

        // Registered Scoped (depends on the Scoped IAuditLogActorProvider). The DbContext
        // attachment happens inside each provider's UseXxxCatalog extension, which calls
        // AddInterceptors with the scoped instance — see SqliteCatalogBuilderExtensions et al.
        services.TryAddScoped<AuditLogSaveChangesInterceptor>();

        // Polar HTTP wrappers — TryAdd so hosts can register their own first.
        services.TryAddScoped<IPolarRefundsApi, PolarClientRefundsApi>();
        services.TryAddScoped<IPolarLicenseKeysApi, PolarClientLicenseKeysApi>();
        services.TryAddScoped<IPolarOrganizationsApi, PolarClientOrganizationsApi>();

        // Inventory event channel — a singleton bounded channel + reader/writer pair.
        // Hosts that want a custom channel topology (e.g. unbounded, or with their own
        // back-pressure policy) register IInventoryEventNotifier before calling this method.
        services.TryAddSingleton(_ => Channel.CreateBounded<SkuStockChanged>(new BoundedChannelOptions(capacity: 1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,        // freshness over historical accuracy
            SingleReader = true,                                   // the sync hosted service is the sole consumer
            SingleWriter = false,                                  // multiple updaters can publish concurrently
        }));
        services.TryAddSingleton<ChannelReader<SkuStockChanged>>(sp => sp.GetRequiredService<Channel<SkuStockChanged>>().Reader);
        services.TryAddSingleton<ChannelWriter<SkuStockChanged>>(sp => sp.GetRequiredService<Channel<SkuStockChanged>>().Writer);
        services.TryAddSingleton<IInventoryEventNotifier, ChannelInventoryEventNotifier>();

        // Publisher (v1.3.E) — HTTP boundary + orchestrator.
        services.TryAddScoped<IPolarPublishingApi, PolarClientPublishingApi>();
        services.AddScoped<IPolarCatalogPublisher, PolarCatalogPublisher>();

        // The service implementations.
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<ILicenseKeyValidator, LicenseKeyValidator>();
        services.AddScoped<IPolarBusinessProfileService, PolarBusinessProfileService>();
        services.AddScoped<IInventoryUpdater, InventoryUpdater>();

        return services;
    }
}
