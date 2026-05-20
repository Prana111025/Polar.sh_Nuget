namespace PolarSharp.PrepaidWallets.Abstractions.Stores;

/// <summary>
/// Per-funding-event bucket state — how many tokens of a given tax-bucket category remain
/// unspent, plus the sequence number of the funding event that originally created them. Included
/// in <see cref="WalletSnapshot"/> so a snapshot+delta load can resume FIFO allocation without
/// re-reading the entire event stream.
/// </summary>
/// <param name="OriginatingFundingEventSequenceNo">Sequence number of the funding (or credit) event that opened this bucket.</param>
/// <param name="Kind">Tax-bucket category.</param>
/// <param name="RemainingTokens">Tokens still unspent in this bucket. Always non-negative; zero-remaining buckets are dropped at snapshot time.</param>
public sealed record FundingBucketState(
    long OriginatingFundingEventSequenceNo,
    FundingSourceKind Kind,
    long RemainingTokens);
