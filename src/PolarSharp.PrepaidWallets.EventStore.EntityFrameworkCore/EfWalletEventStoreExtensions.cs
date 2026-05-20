using Microsoft.Extensions.DependencyInjection;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore;

/// <summary>
/// Provider-agnostic DI extensions for the EF Core wallet event store. Provider packages
/// (<c>.SqlServer</c>, <c>.Sqlite</c>, <c>.PostgreSQL</c>, <c>.MariaDb</c>, <c>.CosmosDb</c>)
/// register a concrete <see cref="WalletEventStoreDbContext"/> subclass and then call into the
/// generic registration below.
/// </summary>
public static class EfWalletEventStoreExtensions
{
    /// <summary>
    /// Register <see cref="EfWalletEventStore"/> and <see cref="EfWalletSnapshotStore"/> against
    /// a host-supplied concrete <see cref="WalletEventStoreDbContext"/> implementation.
    /// </summary>
    /// <typeparam name="TContext">The host's concrete DbContext subclass.</typeparam>
    /// <param name="services">The DI container.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddEfCoreWalletEventStore<TContext>(this IServiceCollection services)
        where TContext : WalletEventStoreDbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<WalletEventStoreDbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped<IWalletEventStore, EfWalletEventStore>();
        services.AddScoped<IWalletSnapshotStore, EfWalletSnapshotStore>();
        return services;
    }
}
