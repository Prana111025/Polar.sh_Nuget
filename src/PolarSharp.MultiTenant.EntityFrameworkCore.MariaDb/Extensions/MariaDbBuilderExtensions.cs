using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolarSharp;
using PolarSharp.MultiTenant.EntityFrameworkCore.Extensions;
using PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb.Upgrade;
using PolarSharp.MultiTenant.EntityFrameworkCore.Upgrade;

namespace PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb;

/// <summary>MariaDB / MySQL-specific registration extensions for the PolarSharp tenant store.</summary>
public static class MariaDbBuilderExtensions
{
    /// <summary>
    /// Registers a MariaDB- or MySQL-backed EF Core tenant store, replacing the in-memory
    /// tenant registry that <c>AddPolarMultiTenant()</c> wires up by default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MariaDB / MySQL do not expose Postgres-style <c>ROW LEVEL SECURITY</c>, so tenant
    /// isolation on this provider is enforced by the <strong>EF Core global query filter only</strong>
    /// (see <see cref="TenantAwareDbContextBase"/>). A bug or misconfiguration that bypasses
    /// the DbContext (e.g. raw <c>IDbConnection</c> queries) will NOT be caught by a database
    /// policy on this provider — that is the Postgres / SQL Server posture, not the MariaDB
    /// posture. Hosts that require defense-in-depth at the DB layer should choose Postgres or
    /// SQL Server instead.
    /// </para>
    /// <para>
    /// The connection string uses the standard ADO.NET MySQL format
    /// (<c>"Server=...;Database=polar_tenants;User Id=...;Password=..."</c>) and is consumed by
    /// Oracle's <c>MySql.EntityFrameworkCore</c> provider. The same provider works against both
    /// MariaDB ≥ 10.5 and MySQL ≥ 8.0 servers.
    /// </para>
    /// </remarks>
    /// <param name="builder">The PolarSharp infrastructure builder returned by <c>AddPolarMultiTenant()</c>.</param>
    /// <param name="connectionString">The MariaDB / MySQL connection string.</param>
    /// <param name="seedFromAppSettings">
    /// When <see langword="true"/> and the tenants table is empty on first startup, copies
    /// every <c>PolarSharp:MultiTenant:Tenants[*]</c> entry from <c>appsettings.json</c>.
    /// </param>
    /// <returns>The same <see cref="PolarInfrastructureBuilder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarInfrastructure(builder.Configuration)
    ///     .AddPolarMultiTenant()
    ///     .UseMariaDb("Server=mariadb.internal;Database=polar_tenants;User Id=polar;Password=...");
    /// </code>
    /// </example>
    public static PolarInfrastructureBuilder UseMariaDb(
        this PolarInfrastructureBuilder builder,
        string connectionString,
        bool seedFromAppSettings = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        EfTenantStoreBuilderExtensions.AddCoreServices(builder.Services, builder.Configuration);

        builder.Services.AddDbContext<PolarTenantDbContext>(opts =>
        {
            opts.UseMySQL(connectionString, mysql =>
                mysql.MigrationsAssembly(typeof(MariaDbBuilderExtensions).Assembly.GetName().Name));
            // Oracle's MySql.EntityFrameworkCore acquires the EF migrations lock with
            // SELECT GET_LOCK('__EFMigrationsLock', -1). On MySQL the -1 timeout returns 1;
            // on MariaDB the same call returns NULL and the provider's (long)scalar cast
            // throws InvalidCastException, breaking any host that runs MigrateAsync on
            // first start-up. The decorator substitutes a non-negative timeout that returns
            // 1 on both engines while preserving real concurrent-migrator safety. See
            // MariaDbCompatibleHistoryRepository's XML docs for the full background and the
            // exit criterion (Pomelo .NET 10 build OR Oracle upstream fix).
            opts.ReplaceService<IHistoryRepository, MariaDbCompatibleHistoryRepository>();
        });
        builder.Services.AddScoped<IMultiTenantStore<PolarTenantInfo>, EfMultiTenantStore>();

        // MariaDB-specific single-tenant -> MT upgrade migrator. Always registered;
        // only executed when the host has also called AddPolarSingleTenantUpgrade(...).
        builder.Services.TryAddScoped<ISingleTenantUpgradeMigrator, MariaDbSingleTenantUpgradeMigrator>();

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<PolarTenantDbContext>(
                name: "polar-tenant-sql",
                tags: ["polar-sql", "polar-tenant"]);

        if (seedFromAppSettings)
        {
            builder.Services.AddHostedService<AppSettingsSeeder>();
        }

        return builder;
    }
}
