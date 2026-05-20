namespace PolarSharp.PrepaidWallets.Abstractions.Events;

/// <summary>
/// Credits tokens to the wallet for a reason other than a funding payment — operator manual
/// credit, B2B purchase-order credit, promotional grant. Distinct from <see cref="WalletFunded"/>
/// (which always corresponds to a customer payment).
/// </summary>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="SequenceNo">Stream sequence number.</param>
/// <param name="OccurredAt">UTC timestamp.</param>
/// <param name="ActorUserId">The operator that issued the credit.</param>
/// <param name="IdempotencyKey">The originating command's idempotency key.</param>
/// <param name="Amount">The tokens credited.</param>
/// <param name="Reason">Free-form reason for the credit (PO id, ticket reference, etc.).</param>
/// <param name="SourceKind">Tax-bucket category of the credited tokens. Drives the Phase 22.5 WTR framework's discount-vs-revenue classification.</param>
/// <param name="RelatedPurchaseOrderId">Optional PO id when this credit consumes PO authorization.</param>
public sealed record WalletCredited(
    WalletId WalletId,
    long SequenceNo,
    DateTimeOffset OccurredAt,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey,
    TokenAmount Amount,
    string Reason,
    FundingSourceKind SourceKind,
    Option<Guid> RelatedPurchaseOrderId) : IWalletEvent
{
    /// <inheritdoc/>
    public string EventType => "wallet.credited";
}
