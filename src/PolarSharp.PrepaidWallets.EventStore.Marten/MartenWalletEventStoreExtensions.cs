using Marten;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.Serialization;
using Weasel.Core;

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

            // Marten defaults to System.Text.Json without our custom converters for the wallet
            // value types (Option<T>, IdempotencyKey, WalletId, TokenAmount). Without these
            // registrations, every wallet event append throws "Option has no value (it is None)"
            // when System.Text.Json's default reflection-based serializer tries to read .Value on
            // a None option. Wire the same converters the EF Core event store uses so the same
            // events round-trip uniformly across both backends.
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, configure: stj =>
            {
                stj.Converters.Add(new OptionGuidJsonConverter());
                stj.Converters.Add(new OptionLongJsonConverter());
                stj.Converters.Add(new OptionStringJsonConverter());
                stj.Converters.Add(new IdempotencyKeyJsonConverter());
                stj.Converters.Add(new WalletIdJsonConverter());
                stj.Converters.Add(new TokenAmountJsonConverter());
            });
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

            // Snapshot-side tenant index — enables fast "show me every tenant's wallet snapshots
            // as-of right now" queries for the Phase 22.5 WTR framework. The event-side tenant
            // index (cross-tenant event aggregation across date ranges) is a Phase 21 follow-up
            // because Marten's events table needs a tenant_id projection set up explicitly via
            // metadata or a custom multi-stream projection.
            opts.Schema.For<MartenWalletSnapshotDocument>()
                .Index(x => x.TenantId);
        });

        services.AddSingleton<IWalletEventStore, MartenWalletEventStore>();
        services.AddSingleton<IWalletSnapshotStore, MartenWalletSnapshotStore>();

        return services;
    }
}
