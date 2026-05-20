using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.Domain;

namespace PolarSharp.PrepaidWallets.Tests;

/// <summary>
/// Proves that loading a wallet from a snapshot + delta-replay produces the same balance and
/// version as a full event-stream replay. This is the invariant Case Study 02 §5 ("Snapshot
/// strategy") depends on.
/// </summary>
public sealed class SnapshotEquivalenceTests
{
    [Fact]
    public void Replay_from_snapshot_matches_full_replay()
    {
        var stepwise = WalletFixture.OpenedWallet(out var id, out var opened);
        var events = new List<Abstractions.Events.IWalletEvent> { opened };

        var fund1 = stepwise.TryFund(WalletFixture.FundCommand(id, 500, "fund-1"), WalletFixture.At).Value;
        stepwise.Apply(fund1);
        events.Add(fund1);

        var debit1 = stepwise.TryDebit(WalletFixture.DebitCommand(id, 100, "debit-1"), WalletFixture.At).Value;
        stepwise.Apply(debit1);
        events.Add(debit1);

        var snapshot = stepwise.ToSnapshot(WalletFixture.At);

        var fund2 = stepwise.TryFund(WalletFixture.FundCommand(id, 200, "fund-2"), WalletFixture.At).Value;
        stepwise.Apply(fund2);
        events.Add(fund2);

        var debit2 = stepwise.TryDebit(WalletFixture.DebitCommand(id, 50, "debit-2"), WalletFixture.At).Value;
        stepwise.Apply(debit2);
        events.Add(debit2);

        var fullReplay = Wallet.Rehydrate(events);
        var fromSnapshot = Wallet.RehydrateFromSnapshot(snapshot, new[] { fund2, debit2 });

        Assert.Equal(fullReplay.Balance, fromSnapshot.Balance);
        Assert.Equal(fullReplay.Version, fromSnapshot.Version);
        Assert.Equal(fullReplay.Status, fromSnapshot.Status);
        Assert.Equal(fullReplay.CustomerId, fromSnapshot.CustomerId);
        Assert.Equal(fullReplay.Currency, fromSnapshot.Currency);
    }

    [Fact]
    public void RehydrateFromSnapshot_rejects_event_with_sequence_at_or_before_snapshot()
    {
        var stepwise = WalletFixture.OpenedWallet(out var id, out _);
        var fund = stepwise.TryFund(WalletFixture.FundCommand(id, 500), WalletFixture.At).Value;
        stepwise.Apply(fund);

        var snapshot = stepwise.ToSnapshot(WalletFixture.At);

        // sequence number 2 is the fund event that's already in the snapshot — cannot replay it
        Assert.Throws<ArgumentException>(() => Wallet.RehydrateFromSnapshot(snapshot, new[] { fund }));
    }

    [Fact]
    public void StrideSnapshotPolicy_fires_at_stride_boundary()
    {
        var policy = new StrideSnapshotPolicy(stride: 10);
        Assert.False(policy.ShouldSnapshot(currentVersion: 9, latestSnapshotVersion: 0));
        Assert.True(policy.ShouldSnapshot(currentVersion: 10, latestSnapshotVersion: 0));
        Assert.True(policy.ShouldSnapshot(currentVersion: 20, latestSnapshotVersion: 10));
        Assert.False(policy.ShouldSnapshot(currentVersion: 11, latestSnapshotVersion: 10));
    }

    [Fact]
    public void StrideSnapshotPolicy_defaults_to_50()
    {
        var policy = new StrideSnapshotPolicy();
        Assert.Equal(50, policy.Stride);
    }

    [Fact]
    public void StrideSnapshotPolicy_rejects_zero_or_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrideSnapshotPolicy(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrideSnapshotPolicy(-1));
    }
}
