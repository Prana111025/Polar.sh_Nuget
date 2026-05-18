using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.MultiTenant.Identity.Extensions;

namespace PolarSharp.MultiTenant.Identity.MariaDb;

/// <summary>
/// MariaDB / MySQL provider registration for PolarSharp Identity.
/// </summary>
/// <remarks>
/// <para>
/// MariaDB / MySQL do not expose Postgres-style <c>ROW LEVEL SECURITY</c>, so per-tenant
/// isolation of Identity tables is enforced by the <strong>EF Core global query filter only</strong>
/// — there is no DB-layer policy on this provider. See the package README for the security
/// posture trade-off.
/// </para>
/// <para>Three deployment shapes — pick one:</para>
/// <list type="number">
///   <item><description><see cref="UseMariaDb(PolarIdentityBuilder, string)"/> — explicit connection string.</description></item>
///   <item><description><see cref="UseMariaDb(PolarIdentityBuilder, IConfiguration)"/> — read from <c>PolarSharp:Identity:Sql</c> in <c>appsettings.json</c>.</description></item>
///   <item><description><c>HostDbContextRegistration.UseHostDbContext&lt;TContext&gt;()</c> (in the base package) — share the host app's existing DbContext.</description></item>
/// </list>
/// </remarks>
public static class MariaDbIdentityBuilderExtensions
{
    /// <summary>Registers a MariaDB-backed <see cref="PolarUserDbContext"/> using an explicit connection string.</summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="connectionString">ADO.NET-format MariaDB / MySQL connection string.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarIdentity(builder.Configuration)
    ///     .UseMariaDb("Server=mariadb.internal;Database=polar_identity;User Id=app;Password=...")
    ///     .AddCoreIdentityServices();
    /// </code>
    /// </example>
    public static PolarIdentityBuilder UseMariaDb(this PolarIdentityBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        return RegisterDbContext(builder, connectionString);
    }

    /// <summary>Registers a MariaDB-backed <see cref="PolarUserDbContext"/> using a connection string resolved from configuration.</summary>
    /// <param name="builder">The Identity builder returned by <c>AddPolarIdentity()</c>.</param>
    /// <param name="configuration">Application configuration — see <see cref="PolarIdentityOptions.SqlOptions"/> for resolution rules.</param>
    /// <returns>The same builder for chaining.</returns>
    public static PolarIdentityBuilder UseMariaDb(this PolarIdentityBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(PolarIdentityOptions.SectionName).Get<PolarIdentityOptions>() ?? new PolarIdentityOptions();
        var connectionString = IdentityConnectionResolver.Resolve(null, configuration, options.Sql);
        return RegisterDbContext(builder, connectionString);
    }

    private static PolarIdentityBuilder RegisterDbContext(PolarIdentityBuilder builder, string connectionString)
    {
        builder.Services.AddDbContext<PolarUserDbContext>(opts =>
        {
            opts.UseMySQL(connectionString, mysql =>
                mysql.MigrationsAssembly(typeof(MariaDbIdentityBuilderExtensions).Assembly.GetName().Name));
            // MariaDB-compatibility for the EF migrations lock. See
            // MariaDbCompatibleHistoryRepository in the base MariaDb tenant package for
            // why every MariaDb provider package needs this replacement.
            opts.ReplaceService<IHistoryRepository, MariaDbCompatibleHistoryRepository>();
        });

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<PolarUserDbContext>(
                name: "polar-identity-sql",
                tags: ["polar-sql", "polar-identity"]);

        builder.AddCoreIdentityServices();
        return builder;
    }
}
