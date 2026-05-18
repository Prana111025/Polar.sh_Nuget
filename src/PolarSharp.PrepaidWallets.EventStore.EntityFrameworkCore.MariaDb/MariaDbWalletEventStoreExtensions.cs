using Microsoft.Extensions.DependencyInjection;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.MariaDb;

/// <summary>
/// MariaDB / MySQL provider for the wallet event store. App-layer query filter only
/// (MariaDB lacks Postgres-style RLS); same posture as v1.3 MariaDB tenant store.
/// </summary>
/// <remarks>
/// <para>Phase 21 ships the registration scaffold; full DbContext + migrations land in Phase 21.x.</para>
/// <para>
/// When the wallet event store DbContext lands in Phase 21.x, its
/// <c>AddDbContext&lt;WalletEventStoreDbContext&gt;(opts =&gt; ...)</c> registration MUST also
/// call <c>opts.ReplaceService&lt;IHistoryRepository, MariaDbCompatibleHistoryRepository&gt;()</c>
/// (from <c>PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb</c>) to apply the
/// Oracle-provider GET_LOCK MariaDB workaround. Add a <c>ProjectReference</c> on
/// <c>PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb</c> at the same time. The other four
/// MariaDb provider packages already do this — see
/// <c>MariaDbBuilderExtensions.UseMariaDb</c>, <c>MariaDbIdentityBuilderExtensions.RegisterDbContext</c>,
/// <c>MariaDbCatalogBuilderExtensions.UseMariaDbCatalog</c>, and
/// <c>MariaDbReportingExtensions.UseMariaDbReporting</c> for the wiring pattern.
/// </para>
/// </remarks>
public static class MariaDbWalletEventStoreExtensions
{
    /// <summary>Registers MariaDB / MySQL as the wallet event store provider.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="connectionString">ADO.NET-format MariaDB / MySQL connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseMariaDbWalletEventStore(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        return services;
    }
}
