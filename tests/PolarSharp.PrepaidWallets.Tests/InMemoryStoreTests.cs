using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Stores;

namespace PolarSharp.PrepaidWallets.Tests;

public sealed class InMemoryEventStoreTests
{
    [Fact]
    public async Task Append_then_load_returns_events_in_order()
    {
        var store = new InMemoryWalletEventStore();
        var id = WalletId.NewId();
        var e1 = MakeOpened(id, seq: 1, keySuffix: "k1");
        var e2 = MakeFunded(id, seq: 2, keySuffix: "k2");

        var r1 = await store.AppendAsync(e1, expectedCurrentVersion: 0);
        Assert.Equal(1, r1.ResultingVersion);

        var r2 = await store.AppendAsync(e2, expectedCurrentVersion: 1);
        Assert.Equal(2, r2.ResultingVersion);

        var loaded = await store.LoadAsync(id, fromSequenceNoInclusive: 1);
        Assert.Equal(2, loaded.Count);
        Assert.Equal(1, loaded[0].SequenceNo);
        Assert.Equal(2, loaded[1].SequenceNo);
    }

    [Fact]
    public async Task Concurrency_conflict_throws()
    {
        var store = new InMemoryWalletEventStore();
        var id = WalletId.NewId();
        await store.AppendAsync(MakeOpened(id, seq: 1, keySuffix: "k1"), expectedCurrentVersion: 0);

        // second writer thinks the stream is still empty
        await Assert.ThrowsAsync<WalletConcurrencyConflictException>(
            () => store.AppendAsync(MakeFunded(id, seq: 2, keySuffix: "k2"), expectedCurrentVersion: 0));
    }

    [Fact]
    public async Task Idempotency_replay_returns_original_event_without_appending()
    {
        var store = new InMemoryWalletEventStore();
        var id = WalletId.NewId();
        var first = MakeOpened(id, seq: 1, keySuffix: "same-key");
        await store.AppendAsync(first, expectedCurrentVersion: 0);

        var replay = MakeOpened(id, seq: 1, keySuffix: "same-key");
        var outcome = await store.AppendAsync(replay, expectedCurrentVersion: 0);

        Assert.True(outcome.WasIdempotencyReplay);
        Assert.Same(first, outcome.Event);

        var loaded = await store.LoadAsync(id, 1);
        Assert.Single(loaded);
    }

    [Fact]
    public async Task Idempotency_with_mismatched_payload_throws()
    {
        var store = new InMemoryWalletEventStore();
        var id = WalletId.NewId();
        await store.AppendAsync(MakeOpened(id, seq: 1, keySuffix: "same-key"), expectedCurrentVersion: 0);

        // same key but different event type
        var conflicting = MakeFunded(id, seq: 1, keySuffix: "same-key");
        await Assert.ThrowsAsync<IdempotencyKeyMismatchException>(
            () => store.AppendAsync(conflicting, expectedCurrentVersion: 0));
    }

    [Fact]
    public async Task LoadAsync_with_offset_filters_earlier_events()
    {
        var store = new InMemoryWalletEventStore();
        var id = WalletId.NewId();
        await store.AppendAsync(MakeOpened(id, seq: 1, keySuffix: "k1"), 0);
        await store.AppendAsync(MakeFunded(id, seq: 2, keySuffix: "k2"), 1);
        await store.AppendAsync(MakeFunded(id, seq: 3, keySuffix: "k3"), 2);

        var loaded = await store.LoadAsync(id, fromSequenceNoInclusive: 2);
        Assert.Equal(2, loaded.Count);
        Assert.Equal(2, loaded[0].SequenceNo);
        Assert.Equal(3, loaded[1].SequenceNo);
    }

    [Fact]
    public async Task LoadAsync_unknown_wallet_returns_empty()
    {
        var store = new InMemoryWalletEventStore();
        var loaded = await store.LoadAsync(WalletId.NewId(), 1);
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task Multiple_wallets_do_not_collide()
    {
        var store = new InMemoryWalletEventStore();
        var a = WalletId.NewId();
        var b = WalletId.NewId();

        await store.AppendAsync(MakeOpened(a, 1, "a-open"), 0);
        await store.AppendAsync(MakeOpened(b, 1, "b-open"), 0);

        Assert.Equal(2, store.WalletCount);
        Assert.Single(await store.LoadAsync(a, 1));
        Assert.Single(await store.LoadAsync(b, 1));
    }

    private static WalletOpened MakeOpened(WalletId id, long seq, string keySuffix) =>
        new(
            id,
            seq,
            WalletFixture.At,
            WalletFixture.Actor,
            WalletFixture.Key(keySuffix),
            WalletFixture.Customer,
            Option<Guid>.None,
            "USD");

    private static WalletFunded MakeFunded(WalletId id, long seq, string keySuffix) =>
        new(
            id,
            seq,
            WalletFixture.At,
            WalletFixture.Actor,
            WalletFixture.Key(keySuffix),
            new TokenAmount(100),
            TokenAmount.Zero,
            FundingSource.Manual("test"),
            10_000, 300, 200, 500, 9_000,
            "{}");
}

public sealed class InMemorySnapshotStoreTests
{
    [Fact]
    public async Task Save_then_load_round_trips()
    {
        var store = new InMemoryWalletSnapshotStore();
        var snap = MakeSnapshot(version: 5);
        await store.SaveAsync(snap);

        var loaded = await store.LoadLatestAsync(snap.WalletId);
        Assert.True(loaded.TryGetValue(out var s));
        Assert.Equal(5, s.Version);
    }

    [Fact]
    public async Task Save_with_lower_version_keeps_higher()
    {
        var store = new InMemoryWalletSnapshotStore();
        var high = MakeSnapshot(version: 10);
        var low = high with { Version = 5 };

        await store.SaveAsync(high);
        await store.SaveAsync(low);

        var loaded = await store.LoadLatestAsync(high.WalletId);
        Assert.Equal(10, loaded.Value.Version);
    }

    [Fact]
    public async Task LoadLatest_unknown_wallet_returns_none()
    {
        var store = new InMemoryWalletSnapshotStore();
        var loaded = await store.LoadLatestAsync(WalletId.NewId());
        Assert.False(loaded.HasValue);
    }

    private static Abstractions.Stores.WalletSnapshot MakeSnapshot(long version) =>
        new(
            WalletId.NewId(),
            version,
            WalletFixture.Customer,
            Option<Guid>.None,
            "USD",
            new TokenAmount(0),
            WalletStatus.Active,
            WalletFixture.At,
            WalletFixture.At,
            WalletFixture.At);
}
