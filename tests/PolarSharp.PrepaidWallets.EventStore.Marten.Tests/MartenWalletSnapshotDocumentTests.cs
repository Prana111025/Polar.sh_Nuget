using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.EventStore.Marten;

namespace PolarSharp.PrepaidWallets.EventStore.Marten.Tests;

/// <summary>
/// Unit-coverage for the Marten event-store provider's pure types. End-to-end Marten integration
/// (Testcontainers-Postgres + Marten document store) lands in Phase 21 per PLAN.md — Phase 20's
/// unit tests prove the mapping layer round-trips correctly.
/// </summary>
public sealed class MartenWalletSnapshotDocumentTests
{
    [Fact]
    public void FromSnapshot_to_ToSnapshot_roundtrips_all_fields_in_multi_tenant_mode()
    {
        var original = new WalletSnapshot(
            WalletId: WalletId.NewId(),
            Version: 27,
            CustomerId: Guid.NewGuid(),
            TenantId: Option<Guid>.Some(Guid.NewGuid()),
            Currency: "USD",
            Balance: new TokenAmount(15_000),
            Status: WalletStatus.Active,
            OpenedAt: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            LastActivityAt: new DateTimeOffset(2026, 5, 19, 12, 0, 0, TimeSpan.Zero),
            TakenAt: new DateTimeOffset(2026, 5, 19, 12, 30, 0, TimeSpan.Zero));

        var doc = MartenWalletSnapshotDocument.FromSnapshot(original);
        var rt = doc.ToSnapshot();

        Assert.Equal(original, rt);
    }

    [Fact]
    public void FromSnapshot_to_ToSnapshot_roundtrips_in_single_tenant_mode()
    {
        var original = new WalletSnapshot(
            WalletId.NewId(),
            10,
            Guid.NewGuid(),
            Option<Guid>.None,
            "EUR",
            new TokenAmount(0),
            WalletStatus.Closed,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var doc = MartenWalletSnapshotDocument.FromSnapshot(original);

        Assert.Null(doc.TenantId);
        var rt = doc.ToSnapshot();
        Assert.False(rt.TenantId.HasValue);
    }

    [Fact]
    public void FromSnapshot_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => MartenWalletSnapshotDocument.FromSnapshot(null!));
    }
}
