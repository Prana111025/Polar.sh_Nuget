using System.Collections.Concurrent;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

namespace PolarSharp.PrepaidWallets.Stores;

/// <summary>
/// In-memory event store. Useful for unit tests and as a reference implementation showing the
/// optimistic-concurrency + idempotency contracts the production stores must honor.
/// </summary>
/// <remarks>
/// Thread-safe at the granularity of one wallet stream. Multiple wallets can be written in
/// parallel; a single wallet's appends are serialized through a per-stream lock.
/// </remarks>
public sealed class InMemoryWalletEventStore : IWalletEventStore
{
    private readonly ConcurrentDictionary<WalletId, WalletStream> _streams = new();

    /// <inheritdoc/>
    public Task<IReadOnlyList<IWalletEvent>> LoadAsync(
        WalletId walletId,
        long fromSequenceNoInclusive,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_streams.TryGetValue(walletId, out var stream))
        {
            return Task.FromResult<IReadOnlyList<IWalletEvent>>(Array.Empty<IWalletEvent>());
        }

        return Task.FromResult(stream.Snapshot(fromSequenceNoInclusive));
    }

    /// <inheritdoc/>
    public Task<AppendOutcome> AppendAsync(
        IWalletEvent @event,
        long expectedCurrentVersion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ct.ThrowIfCancellationRequested();

        var stream = _streams.GetOrAdd(@event.WalletId, _ => new WalletStream());
        var outcome = stream.Append(@event, expectedCurrentVersion);
        return Task.FromResult(outcome);
    }

    /// <summary>Diagnostic — returns the number of wallets the store knows about.</summary>
    public int WalletCount => _streams.Count;

    private sealed class WalletStream
    {
        private readonly object _gate = new();
        private readonly List<IWalletEvent> _events = new();
        private readonly Dictionary<string, IWalletEvent> _byIdempotencyKey = new(StringComparer.Ordinal);

        public IReadOnlyList<IWalletEvent> Snapshot(long fromSequenceNoInclusive)
        {
            lock (_gate)
            {
                if (fromSequenceNoInclusive <= 1)
                {
                    return _events.ToArray();
                }

                return _events
                    .Where(e => e.SequenceNo >= fromSequenceNoInclusive)
                    .ToArray();
            }
        }

        public AppendOutcome Append(IWalletEvent @event, long expectedCurrentVersion)
        {
            lock (_gate)
            {
                var key = @event.IdempotencyKey.Value;
                if (_byIdempotencyKey.TryGetValue(key, out var previous))
                {
                    if (!IdempotentEqual(previous, @event))
                    {
                        throw new IdempotencyKeyMismatchException(@event.WalletId, @event.IdempotencyKey);
                    }

                    return new AppendOutcome(previous, _events.Count, WasIdempotencyReplay: true);
                }

                var currentVersion = _events.Count;
                if (expectedCurrentVersion != currentVersion)
                {
                    throw new WalletConcurrencyConflictException(
                        @event.WalletId,
                        expectedCurrentVersion,
                        currentVersion);
                }

                if (@event.SequenceNo != currentVersion + 1)
                {
                    throw new ArgumentException(
                        $"Event sequence number {@event.SequenceNo} does not equal stream version + 1 "
                            + $"({currentVersion + 1}).",
                        nameof(@event));
                }

                _events.Add(@event);
                _byIdempotencyKey.Add(key, @event);
                return new AppendOutcome(@event, _events.Count, WasIdempotencyReplay: false);
            }
        }

        private static bool IdempotentEqual(IWalletEvent first, IWalletEvent second)
        {
            if (first.GetType() != second.GetType())
            {
                return false;
            }

            // Records use structural equality; the sequence number / timestamp recorded at the
            // first attempt is part of the "original outcome" so we ignore them when deciding
            // whether the *payloads* are equivalent.
            return RecordPayloadEqual(first, second);
        }

        private static bool RecordPayloadEqual(IWalletEvent a, IWalletEvent b) =>
            (a, b) switch
            {
                (WalletOpened x, WalletOpened y) =>
                    x.CustomerId == y.CustomerId && x.TenantId == y.TenantId && x.Currency == y.Currency,
                (WalletFunded x, WalletFunded y) =>
                    x.Amount == y.Amount
                        && x.BonusTokens == y.BonusTokens
                        && x.Source == y.Source
                        && x.SourceKind == y.SourceKind
                        && x.CustomerChargedAmountCents == y.CustomerChargedAmountCents
                        && x.ProcessorFeeCents == y.ProcessorFeeCents
                        && x.SaaSProfitCents == y.SaaSProfitCents
                        && x.TenantAbsorbedAmountCents == y.TenantAbsorbedAmountCents
                        && x.TenantNetAmountCents == y.TenantNetAmountCents,
                (WalletDebited x, WalletDebited y) =>
                    x.Amount == y.Amount
                        && x.TargetKind == y.TargetKind
                        && x.TargetId == y.TargetId,
                (WalletCredited x, WalletCredited y) =>
                    x.Amount == y.Amount
                        && x.Reason == y.Reason
                        && x.SourceKind == y.SourceKind
                        && x.RelatedPurchaseOrderId == y.RelatedPurchaseOrderId,
                (WalletRefunded x, WalletRefunded y) =>
                    x.TokensRefunded == y.TokensRefunded
                        && x.OriginalFundingSequenceNo == y.OriginalFundingSequenceNo
                        && x.CustomerRefundedAmountCents == y.CustomerRefundedAmountCents,
                (WalletFrozen x, WalletFrozen y) => x.Reason == y.Reason,
                (WalletUnfrozen, WalletUnfrozen) => true,
                (WalletClosed x, WalletClosed y) => x.Reason == y.Reason,
                _ => false,
            };
    }
}
