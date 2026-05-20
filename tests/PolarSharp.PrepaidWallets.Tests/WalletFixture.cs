using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Commands;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Domain;

namespace PolarSharp.PrepaidWallets.Tests;

/// <summary>
/// Test helpers — minimal wallet construction and standard command builders. Keeps individual
/// tests focused on the invariant under test rather than command-payload ceremony.
/// </summary>
internal static class WalletFixture
{
    public static readonly Guid Actor = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Customer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly DateTimeOffset At = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    public static IdempotencyKey Key(string suffix) => IdempotencyKey.Create($"test-{suffix}");

    public static OpenWalletCommand OpenCommand(WalletId? id = null) =>
        new(
            id ?? WalletId.NewId(),
            Customer,
            Option<Guid>.None,
            "USD",
            Actor,
            Key("open"));

    public static FundWalletCommand FundCommand(
        WalletId id,
        long amount,
        string keySuffix = "fund-1",
        long bonus = 0,
        FundingSourceKind sourceKind = FundingSourceKind.CustomerCashFunded) =>
        new(
            id,
            new TokenAmount(amount),
            new TokenAmount(bonus),
            FundingSource.Polar($"order-{keySuffix}"),
            sourceKind,
            CustomerChargedAmountCents: 25_000,
            ProcessorFeeCents: 750,
            SaaSProfitCents: 500,
            TenantAbsorbedAmountCents: 1250,
            TenantNetAmountCents: 22_500,
            FundingTermsSnapshotJson: "{\"refundDays\":180,\"surchargePct\":10}",
            Actor,
            Key(keySuffix));

    public static DebitWalletCommand DebitCommand(WalletId id, long amount, string keySuffix = "debit-1") =>
        new(
            id,
            new TokenAmount(amount),
            "order_line",
            "OL-001",
            Actor,
            Key(keySuffix));

    public static CreditWalletCommand CreditCommand(
        WalletId id,
        long amount,
        string keySuffix = "credit-1",
        FundingSourceKind sourceKind = FundingSourceKind.TenantPromotionalGrant) =>
        new(
            id,
            new TokenAmount(amount),
            "PO-123",
            sourceKind,
            Option<Guid>.None,
            Actor,
            Key(keySuffix));

    public static FreezeWalletCommand FreezeCommand(WalletId id, string keySuffix = "freeze-1") =>
        new(id, "fraud-hold", Actor, Key(keySuffix));

    public static UnfreezeWalletCommand UnfreezeCommand(WalletId id, string keySuffix = "unfreeze-1") =>
        new(id, Actor, Key(keySuffix));

    public static CloseWalletCommand CloseCommand(WalletId id, string keySuffix = "close-1") =>
        new(id, "customer-deletion-request", Actor, Key(keySuffix));

    public static Wallet OpenedWallet(out WalletId id, out IWalletEvent opened)
    {
        id = WalletId.NewId();
        var wallet = new Wallet();
        var openResult = wallet.TryOpen(OpenCommand(id), At);
        opened = openResult.Value;
        wallet.Apply(opened);
        return wallet;
    }
}
