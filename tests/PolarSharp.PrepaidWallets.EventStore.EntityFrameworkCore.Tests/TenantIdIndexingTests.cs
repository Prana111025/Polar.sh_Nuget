using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore;
using PolarSharp.PrepaidWallets.Serialization;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.Tests;

/// <summary>
/// Tests for the <c>tenant_id</c> denormalization on <c>wallet_events</c> + the matching index,
/// added per the coordination note from the main session (2026-05-20) so the Phase 22.5 WTR
/// tax-aggregation queries are indexable.
/// </summary>
public sealed class TenantIdIndexingTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Customer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<SqliteWalletEventStoreDbContext> _options;
    private readonly JsonWalletEventSerializer _serializer = new();

    public TenantIdIndexingTests()
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
    public async Task WalletOpened_event_records_its_TenantId_on_the_row()
    {
        var id = WalletId.NewId();
        var opened = new WalletOpened(
            id, 1, At, Actor, IdempotencyKey.Create("test-open"),
            Customer, Option<Guid>.Some(Tenant), "USD");

        using (var db = NewContext())
        {
            await new EfWalletEventStore(db, _serializer).AppendAsync(opened, 0);
        }

        using (var db = NewContext())
        {
            var row = db.WalletEvents.AsNoTracking()
                .Single(e => e.WalletId == id.Value && e.SequenceNo == 1);
            Assert.Equal(Tenant, row.TenantId);
        }
    }

    [Fact]
    public async Task Subsequent_events_inherit_tenant_id_from_the_WalletOpened_row()
    {
        var id = WalletId.NewId();
        var opened = new WalletOpened(
            id, 1, At, Actor, IdempotencyKey.Create("test-open"),
            Customer, Option<Guid>.Some(Tenant), "USD");
        var funded = new WalletFunded(
            id, 2, At, Actor, IdempotencyKey.Create("test-fund"),
            new TokenAmount(100), TokenAmount.Zero,
            FundingSource.Manual("test"),
            FundingSourceKind.CustomerCashFunded,
            10_000, 300, 200, 500, 9_000, "{}");

        using (var db = NewContext())
        {
            var store = new EfWalletEventStore(db, _serializer);
            await store.AppendAsync(opened, 0);
            await store.AppendAsync(funded, 1);
        }

        using (var db = NewContext())
        {
            var rows = db.WalletEvents.AsNoTracking()
                .Where(e => e.WalletId == id.Value)
                .OrderBy(e => e.SequenceNo)
                .ToList();
            Assert.All(rows, r => Assert.Equal(Tenant, r.TenantId));
        }
    }

    [Fact]
    public async Task Single_tenant_wallet_records_null_TenantId()
    {
        var id = WalletId.NewId();
        var opened = new WalletOpened(
            id, 1, At, Actor, IdempotencyKey.Create("test-open"),
            Customer, Option<Guid>.None, "USD");

        using (var db = NewContext())
        {
            await new EfWalletEventStore(db, _serializer).AppendAsync(opened, 0);
        }

        using (var db = NewContext())
        {
            var row = db.WalletEvents.AsNoTracking()
                .Single(e => e.WalletId == id.Value && e.SequenceNo == 1);
            Assert.Null(row.TenantId);
        }
    }

    [Fact]
    public async Task Tenant_scoped_query_returns_only_that_tenants_events()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var walletA = WalletId.NewId();
        var walletB = WalletId.NewId();

        using (var db = NewContext())
        {
            var store = new EfWalletEventStore(db, _serializer);
            await store.AppendAsync(
                new WalletOpened(walletA, 1, At, Actor, IdempotencyKey.Create("ta-open"),
                    Customer, Option<Guid>.Some(tenantA), "USD"),
                0);
            await store.AppendAsync(
                new WalletOpened(walletB, 1, At, Actor, IdempotencyKey.Create("tb-open"),
                    Customer, Option<Guid>.Some(tenantB), "USD"),
                0);
        }

        using (var db = NewContext())
        {
            var tenantAEvents = await db.WalletEvents.AsNoTracking()
                .Where(e => e.TenantId == tenantA)
                .ToListAsync();
            Assert.Single(tenantAEvents);
            Assert.Equal(walletA.Value, tenantAEvents[0].WalletId);
        }
    }
}

public sealed class BucketsJsonCodecTests
{
    [Fact]
    public void Empty_list_round_trips_as_empty_array_marker()
    {
        var json = BucketsJsonCodec.Serialize(Array.Empty<FundingBucketState>());
        Assert.Equal("[]", json);
        Assert.Empty(BucketsJsonCodec.Deserialize(json));
    }

    [Fact]
    public void Bucket_list_round_trips_with_kind_and_remaining()
    {
        var original = new[]
        {
            new FundingBucketState(2, FundingSourceKind.CustomerCashFunded, 500),
            new FundingBucketState(5, FundingSourceKind.TenantPromotionalGrant, 100),
        };
        var json = BucketsJsonCodec.Serialize(original);
        var rt = BucketsJsonCodec.Deserialize(json);
        Assert.Equal(2, rt.Count);
        Assert.Equal(original[0], rt[0]);
        Assert.Equal(original[1], rt[1]);
    }

    [Fact]
    public void Null_or_empty_json_deserializes_to_empty_list()
    {
        Assert.Empty(BucketsJsonCodec.Deserialize(null));
        Assert.Empty(BucketsJsonCodec.Deserialize(""));
        Assert.Empty(BucketsJsonCodec.Deserialize("[]"));
    }
}
