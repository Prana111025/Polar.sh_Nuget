namespace PolarSharp.PrepaidWallets.Abstractions.Events;

/// <summary>
/// Suspends a wallet — funding and debits are rejected; credits and refunds require operator
/// override. Used for fraud holds, customer-service holds, regulatory holds, etc.
/// </summary>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="SequenceNo">Stream sequence number.</param>
/// <param name="OccurredAt">UTC timestamp.</param>
/// <param name="ActorUserId">The operator who froze the wallet.</param>
/// <param name="IdempotencyKey">The originating command's idempotency key.</param>
/// <param name="Reason">Free-form reason for the freeze.</param>
public sealed record WalletFrozen(
    WalletId WalletId,
    long SequenceNo,
    DateTimeOffset OccurredAt,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey,
    string Reason) : IWalletEvent
{
    /// <inheritdoc/>
    public string EventType => "wallet.frozen";
}
