using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore;
using PolarSharp.PrepaidWallets.Serialization;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.Tests;

/// <summary>
/// In-memory SQLite tests for the EF Core provider's event store. SQLite-in-memory is the fastest
/// and most-portable concrete EF Core provider for unit tests; the unique-index behavior matches
/// the real providers (SQL Server, Postgres, etc.) closely enough to exercise the optimistic-
/// concurrency and idempotency paths end-to-end.
/// </summary>
public sealed class EfWalletEventStoreTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Customer = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<SqliteWalletEventStoreDbContext> _options;
    private readonly JsonWalletEventSerializer _serializer = new();

    public EfWalletEventStoreTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _options = new DbContextOptionsBuilder<SqliteWalletEventStoreDbContext>()
            .UseSqlite(_conn)
            .Options;
        using var db = new SqliteWalletEventStoreDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _conn.Dispose();

    private SqliteWalletEventStoreDbContext NewContext() => new(_options);

    [Fact]
    public async Task Append_then_load_round_trips()
    {
        var id = WalletId.NewId();
        var open = MakeOpened(id, 1, "key-open");

        using (var db = NewContext())
        {
            var store = new EfWalletEventStore(db, _serializer);
            var result = await store.AppendAsync(open, expectedCurrentVersion: 0);
            Assert.Equal(1, result.ResultingVersion);
            Assert.False(result.WasIdempotencyReplay);
        }

        using (var db = NewContext())
        {
            var store = new EfWalletEventStore(db, _serializer);
            var events = await store.LoadAsync(id, fromSequenceNoInclusive: 1);
            Assert.Single(events);
            Assert.IsType<WalletOpened>(events[0]);
            Assert.Equal(1, events[0].SequenceNo);
        }
    }

    [Fact]
    public async Task Idempotency_replay_returns_original_event()
    {
        var id = WalletId.NewId();
        var first = MakeOpened(id, 1, "same-key");

        using (var db = NewContext())
        {
            await new EfWalletEventStore(db, _serializer).AppendAsync(first, 0);
        }

        using (var db = NewContext())
        {
            var second = MakeOpened(id, 1, "same-key");
            var result = await new EfWalletEventStore(db, _serializer).AppendAsync(second, 0);
            Assert.True(result.WasIdempotencyReplay);
            Assert.Equal(first.SequenceNo, result.Event.SequenceNo);
        }
    }

    [Fact]
    public async Task Concurrency_conflict_throws()
    {
        var id = WalletId.NewId();
        using (var db = NewContext())
        {
            await new EfWalletEventStore(db, _serializer).AppendAsync(MakeOpened(id, 1, "k1"), 0);
        }

        using (var db = NewContext())
        {
            var stale = MakeFunded(id, 2, "k2");
            // expectedCurrentVersion 0 is stale — current version is 1
            await Assert.ThrowsAsync<WalletConcurrencyConflictException>(
                () => new EfWalletEventStore(db, _serializer).AppendAsync(stale, expectedCurrentVersion: 0));
        }
    }

    [Fact]
    public async Task Idempotency_mismatch_payload_throws()
    {
        var id = WalletId.NewId();
        using (var db = NewContext())
        {
            await new EfWalletEventStore(db, _serializer).AppendAsync(MakeOpened(id, 1, "shared"), 0);
        }

        using (var db = NewContext())
        {
            // Same key, but a different event type — payload mismatch
            var conflicting = MakeFunded(id, 1, "shared");
            await Assert.ThrowsAsync<IdempotencyKeyMismatchException>(
                () => new EfWalletEventStore(db, _serializer).AppendAsync(conflicting, 0));
        }
    }

    [Fact]
    public async Task LoadAsync_with_offset_filters_earlier_events()
    {
        var id = WalletId.NewId();
        using (var db = NewContext())
        {
            var store = new EfWalletEventStore(db, _serializer);
            await store.AppendAsync(MakeOpened(id, 1, "k1"), 0);
            await store.AppendAsync(MakeFunded(id, 2, "k2"), 1);
            await store.AppendAsync(MakeFunded(id, 3, "k3"), 2);
        }

        using (var db = NewContext())
        {
            var events = await new EfWalletEventStore(db, _serializer).LoadAsync(id, fromSequenceNoInclusive: 3);
            Assert.Single(events);
            Assert.Equal(3, events[0].SequenceNo);
        }
    }

    [Fact]
    public async Task Snapshot_store_round_trips_via_SQLite()
    {
        var id = WalletId.NewId();
        var snap = new WalletSnapshot(
            id, 17, Customer, Option<Guid>.None, "USD",
            new TokenAmount(5_000), WalletStatus.Active, At, At, At,
            new[] { new FundingBucketState(2, FundingSourceKind.CustomerCashFunded, 5_000) });

        using (var db = NewContext())
        {
            await new EfWalletSnapshotStore(db).SaveAsync(snap);
        }

        using (var db = NewContext())
        {
            var loaded = await new EfWalletSnapshotStore(db).LoadLatestAsync(id);
            Assert.True(loaded.HasValue);
            Assert.Equal(17, loaded.Value.Version);
            Assert.Equal(5_000, loaded.Value.Balance.Value);
        }
    }

    [Fact]
    public async Task Snapshot_store_LoadLatest_returns_highest_version()
    {
        var id = WalletId.NewId();
        using (var db = NewContext())
        {
            var s = new EfWalletSnapshotStore(db);
            await s.SaveAsync(new WalletSnapshot(id, 5, Customer, Option<Guid>.None, "USD",
                new TokenAmount(100), WalletStatus.Active, At, At, At,
                new[] { new FundingBucketState(2, FundingSourceKind.CustomerCashFunded, 100) }));
            await s.SaveAsync(new WalletSnapshot(id, 10, Customer, Option<Guid>.None, "USD",
                new TokenAmount(200), WalletStatus.Active, At, At, At,
                new[] { new FundingBucketState(7, FundingSourceKind.CustomerCashFunded, 200) }));
        }

        using (var db = NewContext())
        {
            var loaded = await new EfWalletSnapshotStore(db).LoadLatestAsync(id);
            Assert.True(loaded.HasValue);
            Assert.Equal(10, loaded.Value.Version);
            Assert.Equal(200, loaded.Value.Balance.Value);
        }
    }

    [Fact]
    public async Task Snapshot_LoadLatest_unknown_wallet_returns_none()
    {
        using var db = NewContext();
        var loaded = await new EfWalletSnapshotStore(db).LoadLatestAsync(WalletId.NewId());
        Assert.False(loaded.HasValue);
    }

    private static WalletOpened MakeOpened(WalletId id, long seq, string keySuffix) =>
        new(
            id,
            seq,
            At,
            Actor,
            IdempotencyKey.Create($"test-{keySuffix}"),
            Customer,
            Option<Guid>.None,
            "USD");

    private static WalletFunded MakeFunded(WalletId id, long seq, string keySuffix) =>
        new(
            id,
            seq,
            At,
            Actor,
            IdempotencyKey.Create($"test-{keySuffix}"),
            new TokenAmount(100),
            TokenAmount.Zero,
            FundingSource.Manual("test"),
            FundingSourceKind.CustomerCashFunded,
            10_000, 300, 200, 500, 9_000,
            "{}");
}
