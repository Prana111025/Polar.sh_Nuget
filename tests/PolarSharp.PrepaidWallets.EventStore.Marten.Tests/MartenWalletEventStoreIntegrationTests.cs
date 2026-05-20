using Microsoft.Extensions.DependencyInjection;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using Testcontainers.PostgreSql;

namespace PolarSharp.PrepaidWallets.EventStore.Marten.Tests;

/// <summary>
/// End-to-end integration tests for the Marten wallet event store against a real PostgreSQL
/// 17 container spun up by Testcontainers. Closes the explicit deferred gap captured at
/// <c>MartenWalletEventStoreExtensionsTests.cs</c> line 8 ("Wiring against a live Postgres
/// comes in the Phase 21 Testcontainers suite").
/// </summary>
/// <remarks>
/// <para>
/// The companion <see cref="MartenWalletEventStoreExtensionsTests"/> verifies DI registration
/// with a fake connection string. This class proves the registered services actually round-trip
/// events + snapshots against a real Marten + Postgres deployment, including idempotency replay
/// (Marten's lightweight session reads the stream before appending) and optimistic concurrency
/// conflict detection (Marten's <c>expectedVersion</c> argument).
/// </para>
/// <para>
/// One container per test class via <see cref="IAsyncLifetime"/>. Image pinned to
/// <c>postgres:17-alpine</c> for fast cold-start + reproducible behavior across machines.
/// Marten initializes the schema on first use of the document store; no manual migration step
/// is required. Tests must be order-independent because xUnit doesn't guarantee execution
/// order within a class — each test uses a fresh <see cref="WalletId"/> so streams never collide.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "PostgreSQL")]
public sealed class MartenWalletEventStoreIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset At = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Customer = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private PostgreSqlContainer _container = null!;
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.UseMartenWalletEventStore(_container.GetConnectionString(), schemaName: "polar_marten_wallet_test");

        _services = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task Append_then_load_round_trips_via_live_postgres()
    {
        var walletId = WalletId.NewId();
        var open = MakeOpened(walletId, 1, "open-1");

        var store = _services.GetRequiredService<IWalletEventStore>();
        var appendResult = await store.AppendAsync(open, expectedCurrentVersion: 0);

        Assert.Equal(1, appendResult.ResultingVersion);
        Assert.False(appendResult.WasIdempotencyReplay);

        var loaded = await store.LoadAsync(walletId, fromSequenceNoInclusive: 1);
        Assert.Single(loaded);
        Assert.IsType<WalletOpened>(loaded[0]);
        Assert.Equal(1, loaded[0].SequenceNo);
    }

    [Fact]
    public async Task Append_multiple_events_then_load_returns_all_in_order()
    {
        var walletId = WalletId.NewId();
        var store = _services.GetRequiredService<IWalletEventStore>();

        await store.AppendAsync(MakeOpened(walletId, 1, "open-1"), expectedCurrentVersion: 0);
        await store.AppendAsync(MakeFunded(walletId, 2, "fund-1"), expectedCurrentVersion: 1);
        await store.AppendAsync(MakeFunded(walletId, 3, "fund-2"), expectedCurrentVersion: 2);

        var all = await store.LoadAsync(walletId, fromSequenceNoInclusive: 1);
        Assert.Equal(3, all.Count);
        Assert.Equal(1, all[0].SequenceNo);
        Assert.Equal(2, all[1].SequenceNo);
        Assert.Equal(3, all[2].SequenceNo);
    }

    [Fact]
    public async Task LoadAsync_from_offset_filters_earlier_events()
    {
        var walletId = WalletId.NewId();
        var store = _services.GetRequiredService<IWalletEventStore>();

        await store.AppendAsync(MakeOpened(walletId, 1, "open-1"), expectedCurrentVersion: 0);
        await store.AppendAsync(MakeFunded(walletId, 2, "fund-1"), expectedCurrentVersion: 1);
        await store.AppendAsync(MakeFunded(walletId, 3, "fund-2"), expectedCurrentVersion: 2);

        var tail = await store.LoadAsync(walletId, fromSequenceNoInclusive: 3);
        Assert.Single(tail);
        Assert.Equal(3, tail[0].SequenceNo);
    }

    [Fact]
    public async Task Idempotency_replay_returns_original_event_without_writing_a_new_one()
    {
        var walletId = WalletId.NewId();
        var store = _services.GetRequiredService<IWalletEventStore>();

        var first = MakeOpened(walletId, 1, "shared-key");
        await store.AppendAsync(first, expectedCurrentVersion: 0);

        // Second append with the SAME idempotency key — Marten store should detect the prior
        // entry on the stream and return WasIdempotencyReplay = true without writing a new event.
        var replay = MakeOpened(walletId, 1, "shared-key");
        var result = await store.AppendAsync(replay, expectedCurrentVersion: 0);

        Assert.True(result.WasIdempotencyReplay);
        Assert.Equal(first.SequenceNo, result.Event.SequenceNo);

        // Confirm only one event lives on the stream.
        var all = await store.LoadAsync(walletId, fromSequenceNoInclusive: 1);
        Assert.Single(all);
    }

    [Fact]
    public async Task Concurrency_conflict_throws_on_stale_expectedCurrentVersion()
    {
        var walletId = WalletId.NewId();
        var store = _services.GetRequiredService<IWalletEventStore>();

        await store.AppendAsync(MakeOpened(walletId, 1, "open-1"), expectedCurrentVersion: 0);

        // Attempt to append a second event with expectedCurrentVersion=0 — stale, the real
        // current version is 1. The Marten store should detect the conflict and throw.
        var stale = MakeFunded(walletId, 2, "fund-stale");
        await Assert.ThrowsAsync<WalletConcurrencyConflictException>(
            () => store.AppendAsync(stale, expectedCurrentVersion: 0));
    }

    [Fact]
    public async Task Snapshot_store_round_trips_via_live_postgres()
    {
        var walletId = WalletId.NewId();
        var tenantId = Guid.NewGuid();
        var snapshot = new WalletSnapshot(
            walletId,
            Version: 17,
            Customer,
            Option<Guid>.Some(tenantId),
            "USD",
            new TokenAmount(5_000),
            WalletStatus.Active,
            At, At, At,
            new[] { new FundingBucketState(2, FundingSourceKind.CustomerCashFunded, 5_000) });

        var snapshotStore = _services.GetRequiredService<IWalletSnapshotStore>();
        await snapshotStore.SaveAsync(snapshot);

        var loaded = await snapshotStore.LoadLatestAsync(walletId);
        Assert.True(loaded.HasValue);
        Assert.Equal(17, loaded.Value.Version);
        Assert.Equal(5_000, loaded.Value.Balance.Value);
        Assert.True(loaded.Value.TenantId.HasValue);
        Assert.Equal(tenantId, loaded.Value.TenantId.Value);
    }

    [Fact]
    public async Task Snapshot_store_LoadLatestAsync_returns_highest_version_when_multiple_saved()
    {
        var walletId = WalletId.NewId();
        var snapshotStore = _services.GetRequiredService<IWalletSnapshotStore>();

        await snapshotStore.SaveAsync(new WalletSnapshot(
            walletId, 5, Customer, Option<Guid>.None, "USD",
            new TokenAmount(100), WalletStatus.Active, At, At, At,
            new[] { new FundingBucketState(2, FundingSourceKind.CustomerCashFunded, 100) }));

        await snapshotStore.SaveAsync(new WalletSnapshot(
            walletId, 10, Customer, Option<Guid>.None, "USD",
            new TokenAmount(200), WalletStatus.Active, At, At, At,
            new[] { new FundingBucketState(7, FundingSourceKind.CustomerCashFunded, 200) }));

        var loaded = await snapshotStore.LoadLatestAsync(walletId);
        Assert.True(loaded.HasValue);
        Assert.Equal(10, loaded.Value.Version);
        Assert.Equal(200, loaded.Value.Balance.Value);
    }

    [Fact]
    public async Task Snapshot_store_LoadLatestAsync_returns_none_for_unknown_wallet()
    {
        var snapshotStore = _services.GetRequiredService<IWalletSnapshotStore>();
        var loaded = await snapshotStore.LoadLatestAsync(WalletId.NewId());
        Assert.False(loaded.HasValue);
    }

    // --- helpers --------------------------------------------------------------------

    private static WalletOpened MakeOpened(WalletId id, long seq, string keySuffix) =>
        new(
            id,
            seq,
            At,
            Actor,
            IdempotencyKey.Create($"integ-{keySuffix}"),
            Customer,
            Option<Guid>.None,
            "USD");

    private static WalletFunded MakeFunded(WalletId id, long seq, string keySuffix) =>
        new(
            id,
            seq,
            At,
            Actor,
            IdempotencyKey.Create($"integ-{keySuffix}"),
            new TokenAmount(100),
            TokenAmount.Zero,
            FundingSource.Manual("integration-test"),
            FundingSourceKind.CustomerCashFunded,
            10_000, 300, 200, 500, 9_000,
            "{}");
}
