using Microsoft.Extensions.DependencyInjection;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.EventStore.Marten;

namespace PolarSharp.PrepaidWallets.EventStore.Marten.Tests;

/// <summary>
/// Unit tests for the Marten DI registration extension. Wiring against a live Postgres comes in
/// the Phase 21 Testcontainers suite; here we just confirm the extension validates inputs and
/// registers the right service interfaces.
/// </summary>
public sealed class MartenWalletEventStoreExtensionsTests
{
    private const string FakeConnectionString = "Host=localhost;Username=test;Password=test;Database=walletstore";

    [Fact]
    public void Extension_registers_event_and_snapshot_store()
    {
        var services = new ServiceCollection();
        services.UseMartenWalletEventStore(FakeConnectionString);

        Assert.Contains(services, d => d.ServiceType == typeof(IWalletEventStore));
        Assert.Contains(services, d => d.ServiceType == typeof(IWalletSnapshotStore));
    }

    [Fact]
    public void Extension_rejects_null_services()
    {
        Assert.Throws<ArgumentNullException>(() =>
            MartenWalletEventStoreExtensions.UseMartenWalletEventStore(null!, FakeConnectionString));
    }

    [Fact]
    public void Extension_rejects_empty_connection_string()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.UseMartenWalletEventStore(string.Empty));
    }

    [Fact]
    public void Extension_rejects_empty_schema_name()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.UseMartenWalletEventStore(FakeConnectionString, schemaName: string.Empty));
    }
}
