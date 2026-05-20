using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.Tests;

public sealed class EfExtensionsTests
{
    [Fact]
    public void AddEfCoreWalletEventStore_registers_store_and_snapshot_implementations()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SqliteWalletEventStoreDbContext>(opts => opts.UseSqlite("DataSource=:memory:"));
        services.AddEfCoreWalletEventStore<SqliteWalletEventStoreDbContext>();

        Assert.Contains(services, d => d.ServiceType == typeof(IWalletEventStore));
        Assert.Contains(services, d => d.ServiceType == typeof(IWalletSnapshotStore));
        Assert.Contains(services, d => d.ServiceType == typeof(WalletEventStoreDbContext));
    }

    [Fact]
    public void AddEfCoreWalletEventStore_rejects_null_services()
    {
        Assert.Throws<ArgumentNullException>(
            () => EfWalletEventStoreExtensions.AddEfCoreWalletEventStore<SqliteWalletEventStoreDbContext>(null!));
    }
}
