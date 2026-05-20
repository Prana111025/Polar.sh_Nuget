using PolarSharp.PrepaidWallets.Abstractions;

namespace PolarSharp.PrepaidWallets.Abstractions.Tests;

public sealed class ExceptionTests
{
    [Fact]
    public void WalletNotFound_carries_wallet_id()
    {
        var id = WalletId.NewId();
        var ex = new WalletNotFoundException(id);
        Assert.Equal(id, ex.WalletId);
        Assert.Contains(id.ToString(), ex.Message);
    }

    [Fact]
    public void Concurrency_conflict_captures_versions()
    {
        var id = WalletId.NewId();
        var ex = new WalletConcurrencyConflictException(id, expectedSequenceNo: 5, actualSequenceNo: 6);
        Assert.Equal(id, ex.WalletId);
        Assert.Equal(5, ex.ExpectedSequenceNo);
        Assert.Equal(6, ex.ActualSequenceNo);
    }

    [Fact]
    public void Idempotency_mismatch_captures_key()
    {
        var id = WalletId.NewId();
        var key = IdempotencyKey.Create("dup");
        var ex = new IdempotencyKeyMismatchException(id, key);
        Assert.Equal(id, ex.WalletId);
        Assert.Equal(key, ex.Key);
    }

    [Fact]
    public void Unknown_event_type_captures_discriminator()
    {
        var ex = new UnknownWalletEventTypeException("wallet.something_unknown");
        Assert.Equal("wallet.something_unknown", ex.EventType);
    }
}
