namespace PolarSharp.PrepaidWallets.Abstractions.Events;

/// <summary>
/// Refunds wallet tokens. Records the dollars-and-cents refund breakdown — what the customer is
/// owed back on the original payment instrument vs. what is forfeited to the surcharge and SaaS
/// share — per the funding terms snapshotted on the original funding event.
/// </summary>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="SequenceNo">Stream sequence number.</param>
/// <param name="OccurredAt">UTC timestamp.</param>
/// <param name="ActorUserId">The actor that initiated the refund.</param>
/// <param name="IdempotencyKey">The originating command's idempotency key.</param>
/// <param name="TokensRefunded">Tokens debited from the wallet by this refund.</param>
/// <param name="OriginalFundingSequenceNo">The sequence number of the <c>WalletFunded</c> event being partially or wholly refunded.</param>
/// <param name="CustomerRefundedAmountCents">Refund amount returned to the customer's original instrument, in cents.</param>
/// <param name="SurchargeAmountCents">Surcharge withheld per the snapshotted funding terms, in cents.</param>
/// <param name="SaaSShareAmountCents">SaaS share withheld from the refund, in cents.</param>
public sealed record WalletRefunded(
    WalletId WalletId,
    long SequenceNo,
    DateTimeOffset OccurredAt,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey,
    TokenAmount TokensRefunded,
    long OriginalFundingSequenceNo,
    int CustomerRefundedAmountCents,
    int SurchargeAmountCents,
    int SaaSShareAmountCents) : IWalletEvent
{
    /// <inheritdoc/>
    public string EventType => "wallet.refunded";
}
