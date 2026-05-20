namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>
/// A slice of a <see cref="Events.WalletDebited"/> event: "this many tokens came out of this
/// tax-bucket, optionally tied back to the original funding-event id that put those tokens into
/// the bucket." Multiple allocations together describe a single debit that spanned more than one
/// bucket.
/// </summary>
/// <param name="Kind">Tax-bucket category of the tokens being spent.</param>
/// <param name="Tokens">How many tokens of this bucket were spent in this debit. Always positive.</param>
/// <param name="OriginatingFundingEventSequenceNo">
/// Optional back-reference to the <see cref="Events.WalletFunded"/> or <see cref="Events.WalletCredited"/>
/// event that originally credited these tokens. Captured per-bucket so a Phase 22.5 tax-report can
/// trace a refund-as-credit chain back to the original funding payment. Use
/// <see cref="Option{T}.None"/> if the bucket can't be traced to a specific source event (e.g.
/// pre-WTR data migrated from an older wallet ledger).
/// </param>
/// <remarks>
/// The wallet aggregate computes allocations via FIFO by default for Phase 20 (oldest tokens spent
/// first). Pro-rata and specific-identification strategies ship with Phase 22.5 and require
/// per-tenant config plumbing that isn't worth doing now.
/// </remarks>
public sealed record FundingSourceAllocation(
    FundingSourceKind Kind,
    long Tokens,
    Option<long> OriginatingFundingEventSequenceNo);
