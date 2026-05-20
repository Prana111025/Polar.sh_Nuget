namespace PolarSharp.PrepaidWallets.Abstractions.Events;

/// <summary>
/// Terminally closes a wallet. The wallet rejects all subsequent commands; the event log remains
/// queryable for audit purposes.
/// </summary>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="SequenceNo">Stream sequence number.</param>
/// <param name="OccurredAt">UTC timestamp.</param>
/// <param name="ActorUserId">The operator who closed the wallet.</param>
/// <param name="IdempotencyKey">The originating command's idempotency key.</param>
/// <param name="Reason">Free-form reason for the closure (account-deletion request, fraud confirmed, etc.).</param>
public sealed record WalletClosed(
    WalletId WalletId,
    long SequenceNo,
    DateTimeOffset OccurredAt,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey,
    string Reason) : IWalletEvent
{
    /// <inheritdoc/>
    public string EventType => "wallet.closed";
}
