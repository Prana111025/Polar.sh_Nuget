using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.MariaDb;

/// <summary>MariaDB / MySQL provider for the local catalog database.</summary>
/// <remarks>
/// MariaDB / MySQL do not expose Postgres-style <c>ROW LEVEL SECURITY</c>, so per-tenant
/// isolation of catalog tables is enforced by the <strong>EF Core global query filter only</strong>
/// (see <c>PolarSharp.MultiTenant.EntityFrameworkCore.TenantAwareDbContextBase</c>). A bug or misconfiguration that bypasses the
/// DbContext (e.g. raw <c>IDbConnection</c> queries) will NOT be blocked at the database
/// layer on this provider — that is the Postgres / SQL Server posture, not the MariaDB
/// posture. Hosts that require defense-in-depth at the DB layer should choose Postgres or
/// SQL Server instead.
/// </remarks>
public static class MariaDbCatalogBuilderExtensions
{
    /// <summary>Registers a MariaDB-backed <see cref="PolarCatalogDbContext"/>.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="connectionString">ADO.NET-format MariaDB / MySQL connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseMariaDbCatalog(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        services.AddDbContext<PolarCatalogDbContext>((sp, opts) =>
        {
            opts.UseMySQL(connectionString, mysql =>
                mysql.MigrationsAssembly(typeof(MariaDbCatalogBuilderExtensions).Assembly.GetName().Name));
            // MariaDB-compatibility for the EF migrations lock. See
            // MariaDbCompatibleHistoryRepository in the base MariaDb tenant package for
            // why every MariaDb provider package needs this replacement.
            opts.ReplaceService<IHistoryRepository, MariaDbCompatibleHistoryRepository>();
            // V20-013 hook: attach the audit-log interceptor when registered.
            var auditInterceptor = sp.GetService<AuditLogSaveChangesInterceptor>();
            if (auditInterceptor is not null) opts.AddInterceptors(auditInterceptor);
        });
        services.AddHealthChecks()
            .AddDbContextCheck<PolarCatalogDbContext>(name: "polar-catalog-sql", tags: ["polar-sql", "polar-catalog"]);
        return services;
    }

    /// <summary>Registers a MariaDB-backed catalog DbContext, reading the connection string from configuration.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configuration">Application configuration — see <c>PolarSharp:EcommerceStoreManagement:Sql</c>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the connection string is not configured.</exception>
    public static IServiceCollection UseMariaDbCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("PolarSharp:EcommerceStoreManagement:Sql");
        var direct = section["ConnectionString"];
        var named = section["ConnectionStringName"];
        var connectionString = !string.IsNullOrWhiteSpace(direct)
            ? direct
            : !string.IsNullOrWhiteSpace(named) ? configuration.GetConnectionString(named) : null;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Catalog SQL connection string not configured. Set PolarSharp:EcommerceStoreManagement:Sql:ConnectionString or :ConnectionStringName.");
        return services.UseMariaDbCatalog(connectionString);
    }
}
