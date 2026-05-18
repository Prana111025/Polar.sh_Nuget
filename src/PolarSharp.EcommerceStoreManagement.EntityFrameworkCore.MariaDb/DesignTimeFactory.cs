using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStoreManagement.EntityFrameworkCore;
using PolarSharp.MultiTenant;
using PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb;

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.MariaDb;

/// <summary>Design-time factory for the catalog DbContext (MariaDB / MySQL).</summary>
public sealed class PolarCatalogDbContextMariaDbDesignTimeFactory : IDesignTimeDbContextFactory<PolarCatalogDbContext>
{
    /// <inheritdoc/>
    public PolarCatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PolarCatalogDbContext>()
            .UseMySQL(
                "Server=design-time;Database=polar_catalog_design;User Id=design;Password=design;",
                b => b.MigrationsAssembly(typeof(PolarCatalogDbContextMariaDbDesignTimeFactory).Assembly.GetName().Name))
            .ReplaceService<IHistoryRepository, MariaDbCompatibleHistoryRepository>()
            .Options;
        return new PolarCatalogDbContext(options, DesignTimeServices.Build());
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
        private static readonly PolarTenantInfo Tenant = new()
        {
            Id = "design-time-tenant",
            Identifier = "design-time-tenant",
            Name = "Design-time Tenant",
        };
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
