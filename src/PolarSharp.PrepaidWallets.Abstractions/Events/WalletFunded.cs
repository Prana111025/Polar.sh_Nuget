namespace PolarSharp.PrepaidWallets.Abstractions.Events;

/// <summary>
/// A funding event credits tokens to the wallet and records the FULL economic breakdown
/// immutably — customer charged, processor fee, SaaS profit, tenant absorbed, tenant net, tokens
/// credited, plus the funding terms (refund eligibility, surcharge, maintenance fee) snapshotted at
/// the time of funding.
/// </summary>
/// <remarks>
/// <para>
/// Per Case Study 02 sub-pattern 2 ("The wallet event with full economic breakdown"), the
/// fee/profit/tenant-net fields are <strong>required</strong> on the event itself, not added later
/// in a view. Years later, anyone can query the breakdown of a specific payment and get the exact
/// numbers because they're in the event.
/// </para>
/// <para>
/// <paramref name="FundingTermsSnapshotJson"/> is a serialized <c>WalletFundingTerms</c> structure
/// captured at funding time. If the tenant later changes their refund eligibility / surcharge /
/// maintenance-fee policy, the existing tokens stay governed by the snapshotted terms.
/// </para>
/// </remarks>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="SequenceNo">Stream sequence number.</param>
/// <param name="OccurredAt">UTC timestamp.</param>
/// <param name="ActorUserId">The actor on whose behalf the funding happened.</param>
/// <param name="IdempotencyKey">The originating command's idempotency key.</param>
/// <param name="Amount">The tokens credited to the wallet (face value).</param>
/// <param name="BonusTokens">Promotional bonus tokens credited (0 when no bonus applies).</param>
/// <param name="Source">Which funding processor delivered the payment.</param>
/// <param name="CustomerChargedAmountCents">What was charged to the customer's payment instrument, in cents.</param>
/// <param name="ProcessorFeeCents">What the funding processor (Polar / Stripe / PayPal) took, in cents.</param>
/// <param name="SaaSProfitCents">What the SaaS platform took, in cents.</param>
/// <param name="TenantAbsorbedAmountCents">Amount the tenant absorbed (only &gt; 0 in <c>AbsorbAndMarkup</c> mode).</param>
/// <param name="TenantNetAmountCents">What the tenant nets to back the tokens, in cents.</param>
/// <param name="FundingTermsSnapshotJson">Serialized funding terms locked at funding time.</param>
public sealed record WalletFunded(
    WalletId WalletId,
    long SequenceNo,
    DateTimeOffset OccurredAt,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey,
    TokenAmount Amount,
    TokenAmount BonusTokens,
    FundingSource Source,
    int CustomerChargedAmountCents,
    int ProcessorFeeCents,
    int SaaSProfitCents,
    int TenantAbsorbedAmountCents,
    int TenantNetAmountCents,
    string FundingTermsSnapshotJson) : IWalletEvent
{
    /// <inheritdoc/>
    public string EventType => "wallet.funded";
}
