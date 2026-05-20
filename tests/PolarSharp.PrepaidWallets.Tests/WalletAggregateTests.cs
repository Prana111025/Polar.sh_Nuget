using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Commands;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Domain;

namespace PolarSharp.PrepaidWallets.Tests;

public sealed class WalletAggregateTests
{
    [Fact]
    public void TryOpen_on_fresh_wallet_succeeds_with_sequence_1()
    {
        var wallet = new Wallet();
        var result = wallet.TryOpen(WalletFixture.OpenCommand(), WalletFixture.At);

        Assert.True(result.IsSuccess);
        var opened = Assert.IsType<WalletOpened>(result.Value);
        Assert.Equal(1, opened.SequenceNo);
        Assert.Equal("USD", opened.Currency);
    }

    [Fact]
    public void TryOpen_normalizes_currency_to_uppercase()
    {
        var wallet = new Wallet();
        var cmd = WalletFixture.OpenCommand() with { Currency = "eur" };
        var result = wallet.TryOpen(cmd, WalletFixture.At);
        Assert.Equal("EUR", ((WalletOpened)result.Value).Currency);
    }

    [Fact]
    public void TryOpen_rejects_invalid_currency_length()
    {
        var wallet = new Wallet();
        var cmd = WalletFixture.OpenCommand() with { Currency = "DOLLAR" };
        var result = wallet.TryOpen(cmd, WalletFixture.At);
        Assert.True(result.IsFailure);
        Assert.IsType<CommandError.ValidationFailed>(result.Error);
    }

    [Fact]
    public void TryOpen_twice_fails_with_AlreadyExists()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        var result = wallet.TryOpen(WalletFixture.OpenCommand(id), WalletFixture.At);
        Assert.True(result.IsFailure);
        Assert.IsType<CommandError.WalletAlreadyExists>(result.Error);
    }

    [Fact]
    public void TryFund_credits_face_value_plus_bonus()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        var fund = wallet.TryFund(WalletFixture.FundCommand(id, 1000, bonus: 100), WalletFixture.At);
        wallet.Apply(fund.Value);
        Assert.Equal(1100, wallet.Balance.Value);
    }

    [Fact]
    public void TryDebit_reduces_balance()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        wallet.Apply(wallet.TryFund(WalletFixture.FundCommand(id, 1000), WalletFixture.At).Value);
        wallet.Apply(wallet.TryDebit(WalletFixture.DebitCommand(id, 300), WalletFixture.At).Value);
        Assert.Equal(700, wallet.Balance.Value);
    }

    [Fact]
    public void TryDebit_rejects_when_amount_exceeds_balance()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        wallet.Apply(wallet.TryFund(WalletFixture.FundCommand(id, 100), WalletFixture.At).Value);
        var result = wallet.TryDebit(WalletFixture.DebitCommand(id, 200), WalletFixture.At);
        Assert.True(result.IsFailure);
        var err = Assert.IsType<CommandError.InsufficientFunds>(result.Error);
        Assert.Equal(100, err.Balance.Value);
        Assert.Equal(200, err.Requested.Value);
    }

    [Fact]
    public void TryDebit_recorded_balance_matches_post_apply()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        wallet.Apply(wallet.TryFund(WalletFixture.FundCommand(id, 1000), WalletFixture.At).Value);
        var debit = wallet.TryDebit(WalletFixture.DebitCommand(id, 300), WalletFixture.At).Value;
        wallet.Apply(debit);

        var debited = Assert.IsType<WalletDebited>(debit);
        Assert.Equal(wallet.Balance, debited.ResultingBalance);
    }

    [Fact]
    public void TryFund_on_frozen_wallet_is_rejected()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        wallet.Apply(wallet.TryFreeze(WalletFixture.FreezeCommand(id), WalletFixture.At).Value);
        var result = wallet.TryFund(WalletFixture.FundCommand(id, 100), WalletFixture.At);
        Assert.True(result.IsFailure);
        Assert.IsType<CommandError.WalletIsFrozen>(result.Error);
    }

    [Fact]
    public void TryDebit_on_frozen_wallet_is_rejected()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        wallet.Apply(wallet.TryFund(WalletFixture.FundCommand(id, 1000), WalletFixture.At).Value);
        wallet.Apply(wallet.TryFreeze(WalletFixture.FreezeCommand(id), WalletFixture.At).Value);
        var result = wallet.TryDebit(WalletFixture.DebitCommand(id, 100), WalletFixture.At);
        Assert.True(result.IsFailure);
        Assert.IsType<CommandError.WalletIsFrozen>(result.Error);
    }

    [Fact]
    public void TryCredit_on_frozen_wallet_is_permitted()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        wallet.Apply(wallet.TryFreeze(WalletFixture.FreezeCommand(id), WalletFixture.At).Value);
        var result = wallet.TryCredit(WalletFixture.CreditCommand(id, 50), WalletFixture.At);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TryFreeze_twice_returns_AlreadyFrozen()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        wallet.Apply(wallet.TryFreeze(WalletFixture.FreezeCommand(id), WalletFixture.At).Value);
        var result = wallet.TryFreeze(WalletFixture.FreezeCommand(id, "freeze-2"), WalletFixture.At);
        Assert.True(result.IsFailure);
        Assert.IsType<CommandError.WalletAlreadyFrozen>(result.Error);
    }

    [Fact]
    public void TryUnfreeze_on_active_wallet_is_rejected()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        var result = wallet.TryUnfreeze(WalletFixture.UnfreezeCommand(id), WalletFixture.At);
        Assert.True(result.IsFailure);
        Assert.IsType<CommandError.WalletNotFrozen>(result.Error);
    }

    [Fact]
    public void TryUnfreeze_after_freeze_returns_to_active()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        wallet.Apply(wallet.TryFreeze(WalletFixture.FreezeCommand(id), WalletFixture.At).Value);
        wallet.Apply(wallet.TryUnfreeze(WalletFixture.UnfreezeCommand(id), WalletFixture.At).Value);
        Assert.Equal(WalletStatus.Active, wallet.Status);
    }

    [Fact]
    public void TryClose_blocks_all_subsequent_commands()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        wallet.Apply(wallet.TryClose(WalletFixture.CloseCommand(id), WalletFixture.At).Value);

        Assert.IsType<CommandError.WalletIsClosed>(wallet.TryFund(WalletFixture.FundCommand(id, 1), WalletFixture.At).Error);
        Assert.IsType<CommandError.WalletIsClosed>(wallet.TryDebit(WalletFixture.DebitCommand(id, 1), WalletFixture.At).Error);
        Assert.IsType<CommandError.WalletIsClosed>(wallet.TryCredit(WalletFixture.CreditCommand(id, 1), WalletFixture.At).Error);
        Assert.IsType<CommandError.WalletIsClosed>(wallet.TryFreeze(WalletFixture.FreezeCommand(id), WalletFixture.At).Error);
    }

    [Fact]
    public void Commands_against_not_yet_opened_wallet_return_NotFound()
    {
        var wallet = new Wallet();
        var result = wallet.TryFund(WalletFixture.FundCommand(WalletId.NewId(), 100), WalletFixture.At);
        Assert.True(result.IsFailure);
        Assert.IsType<CommandError.WalletNotFound>(result.Error);
    }

    [Fact]
    public void Apply_with_out_of_order_sequence_throws()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out _);
        var skipping = new WalletFunded(
            id,
            SequenceNo: 99,
            WalletFixture.At,
            WalletFixture.Actor,
            WalletFixture.Key("oop"),
            new TokenAmount(10),
            TokenAmount.Zero,
            FundingSource.Manual("test"),
            0, 0, 0, 0, 0,
            "{}");
        Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Apply(skipping));
    }

    [Fact]
    public void Apply_with_wrong_wallet_id_throws()
    {
        var wallet = WalletFixture.OpenedWallet(out _, out _);
        var stray = new WalletFunded(
            WalletId.NewId(),
            SequenceNo: 2,
            WalletFixture.At,
            WalletFixture.Actor,
            WalletFixture.Key("stray"),
            new TokenAmount(10),
            TokenAmount.Zero,
            FundingSource.Manual("test"),
            0, 0, 0, 0, 0,
            "{}");
        Assert.Throws<InvalidOperationException>(() => wallet.Apply(stray));
    }

    [Fact]
    public void HasSeen_returns_true_after_apply()
    {
        var wallet = WalletFixture.OpenedWallet(out var id, out var opened);
        Assert.True(wallet.HasSeen(opened.IdempotencyKey));
        Assert.False(wallet.HasSeen(IdempotencyKey.Create("never-seen")));
    }

    [Fact]
    public void Rehydrate_from_events_reaches_same_state_as_step_by_step()
    {
        var step = WalletFixture.OpenedWallet(out var id, out var opened);
        var fund = step.TryFund(WalletFixture.FundCommand(id, 500), WalletFixture.At).Value;
        step.Apply(fund);
        var debit = step.TryDebit(WalletFixture.DebitCommand(id, 200), WalletFixture.At).Value;
        step.Apply(debit);

        var rehydrated = Wallet.Rehydrate(new[] { opened, fund, debit });
        Assert.Equal(step.Balance, rehydrated.Balance);
        Assert.Equal(step.Version, rehydrated.Version);
        Assert.Equal(step.Status, rehydrated.Status);
    }

    [Fact]
    public void Rehydrate_empty_event_list_throws()
    {
        Assert.Throws<ArgumentException>(() => Wallet.Rehydrate(Array.Empty<IWalletEvent>()));
    }
}
