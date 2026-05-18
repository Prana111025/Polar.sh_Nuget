using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb;
using PolarSharp.Reporting;
using PolarSharp.Reporting.EntityFrameworkCore;

namespace PolarSharp.Reporting.EntityFrameworkCore.MariaDb;

/// <summary>MariaDB / MySQL provider for the Reporting snapshot DbContext.</summary>
/// <remarks>
/// MariaDB / MySQL do not expose Postgres-style <c>ROW LEVEL SECURITY</c>, so per-tenant
/// isolation of reporting snapshot tables is enforced by the EF Core global query filter
/// only. See the package README for the security posture trade-off.
/// </remarks>
public static class MariaDbReportingExtensions
{
    /// <summary>Registers a MariaDB-backed <see cref="PolarReportingDbContext"/> + <see cref="IPolarReportingClient"/>.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="connectionString">ADO.NET-format MariaDB / MySQL connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection UseMariaDbReporting(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        services.AddDbContext<PolarReportingDbContext>(opts =>
        {
            opts.UseMySQL(connectionString, mysql =>
                mysql.MigrationsAssembly(typeof(MariaDbReportingExtensions).Assembly.GetName().Name));
            // MariaDB-compatibility for the EF migrations lock. See
            // MariaDbCompatibleHistoryRepository in the base MariaDb tenant package for
            // why every MariaDb provider package needs this replacement.
            opts.ReplaceService<IHistoryRepository, MariaDbCompatibleHistoryRepository>();
        });
        services.AddHealthChecks()
            .AddDbContextCheck<PolarReportingDbContext>(name: "polar-reporting-sql", tags: ["polar-sql", "polar-reporting"]);
        services.AddScoped<IPolarReportingClient, EfPolarReportingClient>();
        return services;
    }
}

/// <summary>Design-time factory for <c>dotnet ef migrations add</c>.</summary>
public sealed class PolarReportingDbContextMariaDbDesignTimeFactory : IDesignTimeDbContextFactory<PolarReportingDbContext>
{
    /// <inheritdoc/>
    public PolarReportingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarReportingDbContext>()
            .UseMySQL("Server=design-time;Database=polar_reporting_design;User Id=design;Password=design;",
                b => b.MigrationsAssembly(typeof(PolarReportingDbContextMariaDbDesignTimeFactory).Assembly.GetName().Name))
            .ReplaceService<IHistoryRepository, MariaDbCompatibleHistoryRepository>()
            .Options;
        return new PolarReportingDbContext(options, DesignTimeServices.Build());
    }
}

internal static class DesignTimeServices
{
    public static IServiceProvider Build() =>
        new ServiceCollection()
            .AddSingleton<IMultiTenantContextAccessor>(new StubAccessor())
            .BuildServiceProvider();

    private sealed class StubAccessor : IMultiTenantContextAccessor
    {
        private static readonly PolarTenantInfo Tenant = new() { Id = "design-time-tenant", Identifier = "design-time-tenant", Name = "Design" };
        private readonly StubContext _ctx = new(Tenant);
        public IMultiTenantContext MultiTenantContext { get => _ctx; set { /* read-only */ } }
    }

    private sealed class StubContext : IMultiTenantContext
    {
        public StubContext(PolarTenantInfo t) { TenantInfo = t; }
        public ITenantInfo? TenantInfo { get; init; }
        public StrategyInfo? StrategyInfo { get; init; }
        public object? StoreInfo { get; init; }
        public bool IsResolved => true;
    }
}
