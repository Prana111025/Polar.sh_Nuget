using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Commands;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

namespace PolarSharp.PrepaidWallets.Domain;

/// <summary>
/// The wallet aggregate — the canonical event-sourced model of a single prepaid wallet. Every
/// state change goes through one of the <c>Try…</c> methods on this type; each <c>Try…</c> method
/// either returns the event that should be appended or returns a typed
/// <see cref="CommandError"/> when the invariants reject the request.
/// </summary>
/// <remarks>
/// <para>
/// Per Case Study 02 step 3 ("MediatR pipeline with behaviors") and step 1 ("Event-sourced aggregate
/// as the source of truth"): the aggregate state is folded from its events; commands return new
/// events; persistence happens outside the aggregate. The aggregate is the place where invariants
/// live (balance &gt;= 0, no double-debit on the same idempotency key, no funding while frozen,
/// no commands on a closed wallet).
/// </para>
/// <para>
/// The aggregate also tracks per-bucket FIFO state — each funding/credit event opens a bucket
/// tagged with its <see cref="FundingSourceKind"/>; each debit spends from the oldest non-empty
/// bucket first; the per-bucket breakdown is recorded on the <see cref="WalletDebited"/> event
/// so the Phase 22.5 WTR tax framework can compute tax bucket-by-bucket. The buckets are part of
/// aggregate state and snapshot alongside everything else.
/// </para>
/// <para>
/// The class is <c>sealed</c> — invariants live here and nowhere else; subclassing would let a
/// caller add a state-change path that bypasses them.
/// </para>
/// </remarks>
public sealed class Wallet
{
    private readonly HashSet<string> _seenIdempotencyKeys = new(StringComparer.Ordinal);
    private readonly List<MutableBucket> _buckets = new();

    /// <summary>Build a not-yet-opened wallet — the zero state. Used by the command pipeline to apply the first event into.</summary>
    public Wallet()
    {
        Id = WalletId.Empty;
        Currency = string.Empty;
        TenantId = Option<Guid>.None;
    }

    /// <summary>Build a wallet by folding an event history.</summary>
    /// <param name="events">The events to fold, in stream order.</param>
    /// <returns>A <see cref="Wallet"/> reflecting the events.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="events"/> is empty or doesn't start with <see cref="WalletOpened"/>.</exception>
    public static Wallet Rehydrate(IReadOnlyList<IWalletEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            throw new ArgumentException("Cannot rehydrate a wallet from an empty event stream.", nameof(events));
        }

        var wallet = new Wallet();
        foreach (var @event in events)
        {
            wallet.Apply(@event);
        }

        return wallet;
    }

    /// <summary>Build a wallet from a snapshot, then fold the events appended after the snapshot.</summary>
    /// <param name="snapshot">The snapshot to restore from.</param>
    /// <param name="eventsAfterSnapshot">Events whose <see cref="IWalletEvent.SequenceNo"/> is strictly greater than the snapshot version.</param>
    /// <returns>A <see cref="Wallet"/> at the position of the last event applied.</returns>
    public static Wallet RehydrateFromSnapshot(
        WalletSnapshot snapshot,
        IReadOnlyList<IWalletEvent> eventsAfterSnapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(eventsAfterSnapshot);

        var wallet = new Wallet
        {
            Id = snapshot.WalletId,
            CustomerId = snapshot.CustomerId,
            TenantId = snapshot.TenantId,
            Currency = snapshot.Currency,
            Balance = snapshot.Balance,
            Status = snapshot.Status,
            Version = snapshot.Version,
            OpenedAt = snapshot.OpenedAt,
            LastActivityAt = snapshot.LastActivityAt,
        };

        foreach (var bucket in snapshot.RemainingBuckets)
        {
            wallet._buckets.Add(new MutableBucket(
                bucket.OriginatingFundingEventSequenceNo,
                bucket.Kind,
                bucket.RemainingTokens));
        }

        foreach (var @event in eventsAfterSnapshot)
        {
            if (@event.SequenceNo <= snapshot.Version)
            {
                throw new ArgumentException(
                    $"Event sequence {@event.SequenceNo} is not strictly after snapshot version {snapshot.Version}.",
                    nameof(eventsAfterSnapshot));
            }

            wallet.Apply(@event);
        }

        return wallet;
    }

    /// <summary>The wallet identifier.</summary>
    public WalletId Id { get; private set; }

    /// <summary>Customer who owns the wallet.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Tenant scope. <see cref="Option{T}.None"/> in single-tenant deployments.</summary>
    public Option<Guid> TenantId { get; private set; }

    /// <summary>ISO-4217 currency code, locked at open time.</summary>
    public string Currency { get; private set; }

    /// <summary>Current token balance.</summary>
    public TokenAmount Balance { get; private set; } = TokenAmount.Zero;

    /// <summary>Lifecycle status.</summary>
    public WalletStatus Status { get; private set; } = WalletStatus.Active;

    /// <summary>Sequence number of the last applied event. Zero for a not-yet-opened wallet.</summary>
    public long Version { get; private set; }

    /// <summary>UTC timestamp of <see cref="WalletOpened"/>.</summary>
    public DateTimeOffset OpenedAt { get; private set; }

    /// <summary>UTC timestamp of the last appended event.</summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    /// <summary>True iff the wallet has been opened (i.e. at least one event has been applied).</summary>
    public bool IsOpened => Version > 0;

    /// <summary>
    /// Current per-bucket FIFO state — non-empty buckets only, in funding-event sequence order.
    /// Exposed so a host can introspect the breakdown of how the wallet's tokens are made up
    /// (e.g. "what fraction of this wallet's $50 balance came from a tenant promotional grant?").
    /// </summary>
    public IReadOnlyList<FundingBucketState> Buckets => _buckets
        .Where(b => b.Remaining > 0)
        .Select(b => new FundingBucketState(b.SequenceNo, b.Kind, b.Remaining))
        .ToArray();

    /// <summary>Try opening a brand-new wallet. Aggregate must be the zero-state value (no events applied).</summary>
    /// <param name="command">The open command.</param>
    /// <param name="occurredAt">UTC timestamp from the calling pipeline.</param>
    /// <returns>The new event, or a typed error.</returns>
    public Result<IWalletEvent, CommandError> TryOpen(OpenWalletCommand command, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (IsOpened)
        {
            return Result<IWalletEvent, CommandError>.Failure(new CommandError.WalletAlreadyExists(command.WalletId));
        }

        if (string.IsNullOrWhiteSpace(command.Currency) || command.Currency.Length != 3)
        {
            return Result<IWalletEvent, CommandError>.Failure(
                new CommandError.ValidationFailed("Currency must be an ISO-4217 3-letter code."));
        }

        var @event = new WalletOpened(
            command.WalletId,
            SequenceNo: 1,
            occurredAt,
            command.ActorUserId,
            command.IdempotencyKey,
            command.CustomerId,
            command.TenantId,
            command.Currency.ToUpperInvariant());
        return Result<IWalletEvent, CommandError>.Success(@event);
    }

    /// <summary>Try funding the wallet.</summary>
    /// <param name="command">The funding command.</param>
    /// <param name="occurredAt">UTC timestamp from the calling pipeline.</param>
    /// <returns>The new event, or a typed error.</returns>
    public Result<IWalletEvent, CommandError> TryFund(FundWalletCommand command, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (TryEarlyReject(command.WalletId, out var rejection))
        {
            return rejection;
        }

        if (Status == WalletStatus.Frozen)
        {
            return Result<IWalletEvent, CommandError>.Failure(new CommandError.WalletIsFrozen());
        }

        var @event = new WalletFunded(
            Id,
            SequenceNo: Version + 1,
            occurredAt,
            command.ActorUserId,
            command.IdempotencyKey,
            command.Amount,
            command.BonusTokens,
            command.Source,
            command.SourceKind,
            command.CustomerChargedAmountCents,
            command.ProcessorFeeCents,
            command.SaaSProfitCents,
            command.TenantAbsorbedAmountCents,
            command.TenantNetAmountCents,
            command.FundingTermsSnapshotJson);
        return Result<IWalletEvent, CommandError>.Success(@event);
    }

    /// <summary>Try debiting the wallet.</summary>
    /// <param name="command">The debit command.</param>
    /// <param name="occurredAt">UTC timestamp from the calling pipeline.</param>
    /// <returns>The new event, or a typed error. The event carries a per-bucket <c>FundingSources</c> allocation computed via FIFO.</returns>
    public Result<IWalletEvent, CommandError> TryDebit(DebitWalletCommand command, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (TryEarlyReject(command.WalletId, out var rejection))
        {
            return rejection;
        }

        if (Status == WalletStatus.Frozen)
        {
            return Result<IWalletEvent, CommandError>.Failure(new CommandError.WalletIsFrozen());
        }

        if (command.Amount > Balance)
        {
            return Result<IWalletEvent, CommandError>.Failure(
                new CommandError.InsufficientFunds(Balance, command.Amount));
        }

        var allocations = AllocateFifo(command.Amount.Value);
        var newBalance = Balance - command.Amount;
        var @event = new WalletDebited(
            Id,
            SequenceNo: Version + 1,
            occurredAt,
            command.ActorUserId,
            command.IdempotencyKey,
            command.Amount,
            command.TargetKind,
            command.TargetId,
            newBalance,
            allocations);
        return Result<IWalletEvent, CommandError>.Success(@event);
    }

    /// <summary>Try crediting the wallet (non-funding credit, e.g. PO credit or operator manual credit).</summary>
    /// <param name="command">The credit command.</param>
    /// <param name="occurredAt">UTC timestamp from the calling pipeline.</param>
    /// <returns>The new event, or a typed error.</returns>
    public Result<IWalletEvent, CommandError> TryCredit(CreditWalletCommand command, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (TryEarlyReject(command.WalletId, out var rejection))
        {
            return rejection;
        }

        // Credits are allowed in Active and Frozen states (operator override on Frozen) — only
        // Closed wallets reject credits, and TryEarlyReject already enforced that.
        var @event = new WalletCredited(
            Id,
            SequenceNo: Version + 1,
            occurredAt,
            command.ActorUserId,
            command.IdempotencyKey,
            command.Amount,
            command.Reason,
            command.SourceKind,
            command.RelatedPurchaseOrderId);
        return Result<IWalletEvent, CommandError>.Success(@event);
    }

    /// <summary>Try refunding tokens from the wallet.</summary>
    /// <param name="command">The refund command.</param>
    /// <param name="occurredAt">UTC timestamp from the calling pipeline.</param>
    /// <returns>The new event, or a typed error.</returns>
    public Result<IWalletEvent, CommandError> TryRefund(RefundWalletCommand command, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (TryEarlyReject(command.WalletId, out var rejection))
        {
            return rejection;
        }

        if (command.TokensRefunded > Balance)
        {
            return Result<IWalletEvent, CommandError>.Failure(
                new CommandError.InsufficientFunds(Balance, command.TokensRefunded));
        }

        var @event = new WalletRefunded(
            Id,
            SequenceNo: Version + 1,
            occurredAt,
            command.ActorUserId,
            command.IdempotencyKey,
            command.TokensRefunded,
            command.OriginalFundingSequenceNo,
            command.CustomerRefundedAmountCents,
            command.SurchargeAmountCents,
            command.SaaSShareAmountCents);
        return Result<IWalletEvent, CommandError>.Success(@event);
    }

    /// <summary>Try freezing the wallet.</summary>
    /// <param name="command">The freeze command.</param>
    /// <param name="occurredAt">UTC timestamp from the calling pipeline.</param>
    /// <returns>The new event, or a typed error.</returns>
    public Result<IWalletEvent, CommandError> TryFreeze(FreezeWalletCommand command, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (TryEarlyReject(command.WalletId, out var rejection))
        {
            return rejection;
        }

        if (Status == WalletStatus.Frozen)
        {
            return Result<IWalletEvent, CommandError>.Failure(new CommandError.WalletAlreadyFrozen());
        }

        var @event = new WalletFrozen(
            Id,
            SequenceNo: Version + 1,
            occurredAt,
            command.ActorUserId,
            command.IdempotencyKey,
            command.Reason);
        return Result<IWalletEvent, CommandError>.Success(@event);
    }

    /// <summary>Try unfreezing the wallet.</summary>
    /// <param name="command">The unfreeze command.</param>
    /// <param name="occurredAt">UTC timestamp from the calling pipeline.</param>
    /// <returns>The new event, or a typed error.</returns>
    public Result<IWalletEvent, CommandError> TryUnfreeze(UnfreezeWalletCommand command, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (TryEarlyReject(command.WalletId, out var rejection))
        {
            return rejection;
        }

        if (Status != WalletStatus.Frozen)
        {
            return Result<IWalletEvent, CommandError>.Failure(new CommandError.WalletNotFrozen());
        }

        var @event = new WalletUnfrozen(
            Id,
            SequenceNo: Version + 1,
            occurredAt,
            command.ActorUserId,
            command.IdempotencyKey);
        return Result<IWalletEvent, CommandError>.Success(@event);
    }

    /// <summary>Try closing the wallet.</summary>
    /// <param name="command">The close command.</param>
    /// <param name="occurredAt">UTC timestamp from the calling pipeline.</param>
    /// <returns>The new event, or a typed error.</returns>
    public Result<IWalletEvent, CommandError> TryClose(CloseWalletCommand command, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (TryEarlyReject(command.WalletId, out var rejection))
        {
            return rejection;
        }

        var @event = new WalletClosed(
            Id,
            SequenceNo: Version + 1,
            occurredAt,
            command.ActorUserId,
            command.IdempotencyKey,
            command.Reason);
        return Result<IWalletEvent, CommandError>.Success(@event);
    }

    /// <summary>Apply an event to the aggregate (fold step).</summary>
    /// <param name="event">The event.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the event's sequence number does not equal <c>Version + 1</c>.</exception>
    public void Apply(IWalletEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (@event is WalletOpened opened)
        {
            if (IsOpened)
            {
                throw new InvalidOperationException("Wallet has already been opened; cannot apply a second WalletOpened.");
            }

            Id = opened.WalletId;
            CustomerId = opened.CustomerId;
            TenantId = opened.TenantId;
            Currency = opened.Currency;
            OpenedAt = opened.OccurredAt;
        }
        else if (!IsOpened)
        {
            throw new InvalidOperationException(
                $"Cannot apply '{@event.EventType}' to a wallet that has not been opened.");
        }
        else if (@event.WalletId != Id)
        {
            throw new InvalidOperationException(
                $"Event WalletId '{@event.WalletId}' does not match aggregate id '{Id}'.");
        }

        if (@event.SequenceNo != Version + 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(@event),
                $"Event sequence number {@event.SequenceNo} does not equal aggregate version + 1 ({Version + 1}).");
        }

        ApplyEffect(@event);

        Version = @event.SequenceNo;
        LastActivityAt = @event.OccurredAt;
        _seenIdempotencyKeys.Add(@event.IdempotencyKey.Value);
    }

    /// <summary>Returns <see langword="true"/> if the wallet has already recorded the given idempotency key.</summary>
    /// <param name="key">The idempotency key to check.</param>
    public bool HasSeen(IdempotencyKey key) => _seenIdempotencyKeys.Contains(key.Value);

    private void ApplyEffect(IWalletEvent @event)
    {
        switch (@event)
        {
            case WalletOpened:
                // already handled above
                break;
            case WalletFunded funded:
                Balance += funded.Amount + funded.BonusTokens;
                var fundedTokens = funded.Amount.Value + funded.BonusTokens.Value;
                if (fundedTokens > 0)
                {
                    _buckets.Add(new MutableBucket(funded.SequenceNo, funded.SourceKind, fundedTokens));
                }
                break;
            case WalletDebited debited:
                Balance -= debited.Amount;
                ConsumeBucketsForDebit(debited);
                break;
            case WalletCredited credited:
                Balance += credited.Amount;
                if (credited.Amount.Value > 0)
                {
                    _buckets.Add(new MutableBucket(credited.SequenceNo, credited.SourceKind, credited.Amount.Value));
                }
                break;
            case WalletRefunded refunded:
                Balance -= refunded.TokensRefunded;
                ConsumeBucketsFifo(refunded.TokensRefunded.Value);
                break;
            case WalletFrozen:
                Status = WalletStatus.Frozen;
                break;
            case WalletUnfrozen:
                Status = WalletStatus.Active;
                break;
            case WalletClosed:
                Status = WalletStatus.Closed;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(@event),
                    $"Unknown wallet event type '{@event.EventType}'.");
        }
    }

    /// <summary>
    /// Compute a FIFO allocation for an upcoming debit without mutating bucket state. Used by
    /// <see cref="TryDebit"/> to fill the event's <c>FundingSources</c> field.
    /// </summary>
    private IReadOnlyList<FundingSourceAllocation> AllocateFifo(long tokensToSpend)
    {
        var allocations = new List<FundingSourceAllocation>();
        var remaining = tokensToSpend;
        foreach (var bucket in _buckets)
        {
            if (remaining == 0)
            {
                break;
            }

            if (bucket.Remaining == 0)
            {
                continue;
            }

            var take = Math.Min(remaining, bucket.Remaining);
            allocations.Add(new FundingSourceAllocation(
                bucket.Kind,
                take,
                Option<long>.Some(bucket.SequenceNo)));
            remaining -= take;
        }

        if (remaining != 0)
        {
            throw new InvalidOperationException(
                $"Bucket state is out of sync with balance: {remaining} tokens could not be allocated despite balance check passing.");
        }

        return allocations;
    }

    /// <summary>Consume buckets for a <see cref="WalletDebited"/> event using the event's recorded allocation list.</summary>
    private void ConsumeBucketsForDebit(WalletDebited debited)
    {
        foreach (var allocation in debited.FundingSources)
        {
            if (!allocation.OriginatingFundingEventSequenceNo.TryGetValue(out var seqNo))
            {
                ConsumeBucketsFifo(allocation.Tokens);
                continue;
            }

            var bucket = _buckets.FirstOrDefault(b => b.SequenceNo == seqNo);
            if (bucket is null || bucket.Remaining < allocation.Tokens)
            {
                throw new InvalidOperationException(
                    $"Cannot apply WalletDebited sequence {debited.SequenceNo}: bucket {seqNo} is missing or has fewer than {allocation.Tokens} tokens.");
            }

            bucket.Remaining -= allocation.Tokens;
        }
    }

    /// <summary>Consume <paramref name="tokens"/> from the FIFO bucket list. Used by refund replay.</summary>
    private void ConsumeBucketsFifo(long tokens)
    {
        var remaining = tokens;
        foreach (var bucket in _buckets)
        {
            if (remaining == 0)
            {
                break;
            }

            if (bucket.Remaining == 0)
            {
                continue;
            }

            var take = Math.Min(remaining, bucket.Remaining);
            bucket.Remaining -= take;
            remaining -= take;
        }

        if (remaining != 0)
        {
            throw new InvalidOperationException(
                $"Bucket state is out of sync: {remaining} tokens could not be drained for a FIFO consume.");
        }
    }

    private bool TryEarlyReject(WalletId expectedId, out Result<IWalletEvent, CommandError> rejection)
    {
        if (!IsOpened)
        {
            rejection = Result<IWalletEvent, CommandError>.Failure(new CommandError.WalletNotFound(expectedId));
            return true;
        }

        if (expectedId != Id)
        {
            rejection = Result<IWalletEvent, CommandError>.Failure(new CommandError.WalletNotFound(expectedId));
            return true;
        }

        if (Status == WalletStatus.Closed)
        {
            rejection = Result<IWalletEvent, CommandError>.Failure(new CommandError.WalletIsClosed());
            return true;
        }

        rejection = default;
        return false;
    }

    /// <summary>
    /// Internal mutable bucket — the aggregate's working state for FIFO allocation. Exposed
    /// outwards as immutable <see cref="FundingBucketState"/> via <see cref="Buckets"/>.
    /// </summary>
    private sealed class MutableBucket
    {
        public MutableBucket(long sequenceNo, FundingSourceKind kind, long remaining)
        {
            SequenceNo = sequenceNo;
            Kind = kind;
            Remaining = remaining;
        }

        public long SequenceNo { get; }
        public FundingSourceKind Kind { get; }
        public long Remaining { get; set; }
    }
}
