namespace PolarSharp.PrepaidWallets.Abstractions.Events;

/// <summary>
/// Debits tokens from the wallet against a target (order line, subscription invoice, PO line item,
/// etc.). The target is identified by a free-form <see cref="TargetKind"/> + <see cref="TargetId"/>
/// pair so the wallet can be debited by any host workflow without the wallet-core needing to know
/// the workflow's domain model.
/// </summary>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="SequenceNo">Stream sequence number.</param>
/// <param name="OccurredAt">UTC timestamp.</param>
/// <param name="ActorUserId">The actor on whose behalf the debit happened (usually the wallet's owner).</param>
/// <param name="IdempotencyKey">The originating command's idempotency key.</param>
/// <param name="Amount">The tokens debited.</param>
/// <param name="TargetKind">The kind of thing the debit pays for (e.g. <c>"order_line"</c>, <c>"subscription_invoice"</c>).</param>
/// <param name="TargetId">The target id (free-form string).</param>
/// <param name="ResultingBalance">Wallet balance after applying this debit. Recorded for audit speed.</param>
public sealed record WalletDebited(
    WalletId WalletId,
    long SequenceNo,
    DateTimeOffset OccurredAt,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey,
    TokenAmount Amount,
    string TargetKind,
    string TargetId,
    TokenAmount ResultingBalance) : IWalletEvent
{
    /// <inheritdoc/>
    public string EventType => "wallet.debited";
}
