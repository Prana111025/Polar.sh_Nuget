using Marten;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

namespace PolarSharp.PrepaidWallets.EventStore.Marten;

/// <summary>
/// Marten-backed event store for the PolarSharp prepaid wallet feature.
/// </summary>
/// <remarks>
/// Marten is the natural fit when the host's database is PostgreSQL — Marten provides
/// native event-sourcing primitives (event streams, projection daemon, snapshot support)
/// that align with the wallet's aggregate model. Hosts on other databases use the EF
/// Core event store + the appropriate provider package.
/// </remarks>
public static class MartenWalletEventStoreExtensions
{
    /// <summary>Registers Marten as the wallet event store backend.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="postgresConnectionString">PostgreSQL connection string for the Marten document store.</param>
    /// <param name="schemaName">Optional Postgres schema name (defaults to <c>"polar_marten_wallet"</c>).</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseMartenWalletEventStore(
        this IServiceCollection services,
        string postgresConnectionString,
        string schemaName = "polar_marten_wallet")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(postgresConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(schemaName);

        services.AddMarten(opts =>
        {
            opts.Connection(postgresConnectionString);
            opts.DatabaseSchemaName = schemaName;
            // Guid stream keys are Marten's default; the wallet uses the WalletId.Value Guid directly.
            opts.Events.AddEventType(typeof(WalletOpened));
            opts.Events.AddEventType(typeof(WalletFunded));
            opts.Events.AddEventType(typeof(WalletDebited));
            opts.Events.AddEventType(typeof(WalletCredited));
            opts.Events.AddEventType(typeof(WalletRefunded));
            opts.Events.AddEventType(typeof(WalletFrozen));
            opts.Events.AddEventType(typeof(WalletUnfrozen));
            opts.Events.AddEventType(typeof(WalletClosed));
            opts.RegisterDocumentType<MartenWalletSnapshotDocument>();
        });

        services.AddSingleton<IWalletEventStore, MartenWalletEventStore>();
        services.AddSingleton<IWalletSnapshotStore, MartenWalletSnapshotStore>();

        return services;
    }
}
