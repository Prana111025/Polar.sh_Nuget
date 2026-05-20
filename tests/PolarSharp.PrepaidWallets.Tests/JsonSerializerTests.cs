using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Serialization;

namespace PolarSharp.PrepaidWallets.Tests;

public sealed class JsonSerializerTests
{
    private readonly JsonWalletEventSerializer _serializer = new();

    [Fact]
    public void WalletOpened_round_trips()
    {
        var original = new WalletOpened(
            WalletId.NewId(),
            1,
            WalletFixture.At,
            WalletFixture.Actor,
            WalletFixture.Key("open"),
            WalletFixture.Customer,
            Option<Guid>.Some(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            "USD");

        var bytes = _serializer.Serialize(original);
        var roundTripped = (WalletOpened)_serializer.Deserialize(bytes.EventType, bytes.PayloadJson);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void WalletFunded_with_full_breakdown_round_trips()
    {
        var original = new WalletFunded(
            WalletId.NewId(),
            2,
            WalletFixture.At,
            WalletFixture.Actor,
            WalletFixture.Key("fund"),
            new TokenAmount(25_000),
            new TokenAmount(2_500),
            FundingSource.Polar("order_test"),
            FundingSourceKind.CustomerCashFunded,
            CustomerChargedAmountCents: 25_000,
            ProcessorFeeCents: 750,
            SaaSProfitCents: 500,
            TenantAbsorbedAmountCents: 1_250,
            TenantNetAmountCents: 22_500,
            FundingTermsSnapshotJson: "{\"refundDays\":180}");
        var bytes = _serializer.Serialize(original);
        var rt = (WalletFunded)_serializer.Deserialize(bytes.EventType, bytes.PayloadJson);
        Assert.Equal(original.Amount, rt.Amount);
        Assert.Equal(original.BonusTokens, rt.BonusTokens);
        Assert.Equal(original.Source, rt.Source);
        Assert.Equal(original.SourceKind, rt.SourceKind);
        Assert.Equal(original.CustomerChargedAmountCents, rt.CustomerChargedAmountCents);
        Assert.Equal(original.ProcessorFeeCents, rt.ProcessorFeeCents);
        Assert.Equal(original.FundingTermsSnapshotJson, rt.FundingTermsSnapshotJson);
    }

    [Fact]
    public void Every_event_kind_has_a_registered_discriminator()
    {
        var id = WalletId.NewId();
        IReadOnlyList<FundingSourceAllocation> allocations =
            new[] { new FundingSourceAllocation(FundingSourceKind.CustomerCashFunded, 1, Option<long>.Some(2)) };
        IWalletEvent[] events =
        [
            new WalletOpened(id, 1, WalletFixture.At, WalletFixture.Actor, WalletFixture.Key("o"), WalletFixture.Customer, Option<Guid>.None, "USD"),
            new WalletFunded(id, 2, WalletFixture.At, WalletFixture.Actor, WalletFixture.Key("f"), new TokenAmount(1), TokenAmount.Zero, FundingSource.Manual("m"), FundingSourceKind.CustomerCashFunded, 0,0,0,0,0, "{}"),
            new WalletDebited(id, 3, WalletFixture.At, WalletFixture.Actor, WalletFixture.Key("d"), new TokenAmount(1), "k", "i", TokenAmount.Zero, allocations),
            new WalletCredited(id, 4, WalletFixture.At, WalletFixture.Actor, WalletFixture.Key("c"), new TokenAmount(1), "r", FundingSourceKind.TenantPromotionalGrant, Option<Guid>.None),
            new WalletRefunded(id, 5, WalletFixture.At, WalletFixture.Actor, WalletFixture.Key("r"), new TokenAmount(1), 2, 0, 0, 0),
            new WalletFrozen(id, 6, WalletFixture.At, WalletFixture.Actor, WalletFixture.Key("fz"), "reason"),
            new WalletUnfrozen(id, 7, WalletFixture.At, WalletFixture.Actor, WalletFixture.Key("uf")),
            new WalletClosed(id, 8, WalletFixture.At, WalletFixture.Actor, WalletFixture.Key("cl"), "reason"),
        ];

        foreach (var e in events)
        {
            var bytes = _serializer.Serialize(e);
            var rt = _serializer.Deserialize(bytes.EventType, bytes.PayloadJson);
            Assert.Equal(e.EventType, rt.EventType);
            Assert.Equal(e.SequenceNo, rt.SequenceNo);
            Assert.Equal(e.WalletId, rt.WalletId);
        }
    }

    [Fact]
    public void Unknown_event_type_throws()
    {
        Assert.Throws<UnknownWalletEventTypeException>(() => _serializer.Deserialize("wallet.gibberish", "{}"));
    }
}
