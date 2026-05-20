using Microsoft.EntityFrameworkCore;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

// EF Core 10 model-snapshot generator runs even in Release builds, calling into the model graph.
// No additional pragmas needed here — the schema is fully declared in OnModelCreating.

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IWalletEventStore"/>. Backs the wallet stream on the
/// <see cref="WalletEventStoreDbContext.WalletEvents"/> table; optimistic concurrency rides on the
/// unique <c>(wallet_id, sequence_no)</c> index; idempotency rides on the unique
/// <c>(wallet_id, idempotency_key)</c> index.
/// </summary>
public sealed class EfWalletEventStore : IWalletEventStore
{
    private readonly WalletEventStoreDbContext _db;
    private readonly IWalletEventSerializer _serializer;

    /// <summary>Construct the store.</summary>
    /// <param name="db">The EF Core context.</param>
    /// <param name="serializer">JSON serializer used for event bodies.</param>
    public EfWalletEventStore(WalletEventStoreDbContext db, IWalletEventSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(serializer);
        _db = db;
        _serializer = serializer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IWalletEvent>> LoadAsync(
        WalletId walletId,
        long fromSequenceNoInclusive,
        CancellationToken ct = default)
    {
        var rows = await _db.WalletEvents
            .AsNoTracking()
            .Where(x => x.WalletId == walletId.Value && x.SequenceNo >= fromSequenceNoInclusive)
            .OrderBy(x => x.SequenceNo)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var result = new IWalletEvent[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            result[i] = _serializer.Deserialize(rows[i].EventType, rows[i].EventPayloadJson);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<AppendOutcome> AppendAsync(
        IWalletEvent @event,
        long expectedCurrentVersion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var keyValue = @event.IdempotencyKey.Value;
        var previousRow = await _db.WalletEvents
            .AsNoTracking()
            .Where(x => x.WalletId == @event.WalletId.Value && x.IdempotencyKey == keyValue)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (previousRow is not null)
        {
            var previousEvent = _serializer.Deserialize(previousRow.EventType, previousRow.EventPayloadJson);
            if (previousEvent.GetType() != @event.GetType())
            {
                throw new IdempotencyKeyMismatchException(@event.WalletId, @event.IdempotencyKey);
            }

            var maxAfter = await _db.WalletEvents
                .AsNoTracking()
                .Where(x => x.WalletId == @event.WalletId.Value)
                .Select(x => (long?)x.SequenceNo)
                .MaxAsync(ct)
                .ConfigureAwait(false);
            return new AppendOutcome(previousEvent, maxAfter ?? 0L, WasIdempotencyReplay: true);
        }

        var observed = await _db.WalletEvents
            .AsNoTracking()
            .Where(x => x.WalletId == @event.WalletId.Value)
            .Select(x => (long?)x.SequenceNo)
            .MaxAsync(ct)
            .ConfigureAwait(false);
        var observedVersion = observed ?? 0L;

        if (observedVersion != expectedCurrentVersion)
        {
            throw new WalletConcurrencyConflictException(
                @event.WalletId,
                expectedCurrentVersion,
                observedVersion);
        }

        var serialized = _serializer.Serialize(@event);
        var tenantId = await ResolveTenantIdAsync(@event, ct).ConfigureAwait(false);
        var row = new WalletEventRecord
        {
            Id = Guid.NewGuid(),
            WalletId = @event.WalletId.Value,
            TenantId = tenantId,
            SequenceNo = @event.SequenceNo,
            EventType = serialized.EventType,
            EventPayloadJson = serialized.PayloadJson,
            IdempotencyKey = @event.IdempotencyKey.Value,
            OccurredAt = @event.OccurredAt,
            ActorUserId = @event.ActorUserId,
        };

        _db.WalletEvents.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // The unique (wallet_id, sequence_no) or (wallet_id, idempotency_key) index hit; translate
            // to the wallet's concurrency-conflict abstraction so the MediatR retry behavior can recover.
            throw new WalletConcurrencyConflictException(
                @event.WalletId,
                expectedCurrentVersion,
                observedVersion + 1);
        }

        return new AppendOutcome(@event, ResultingVersion: @event.SequenceNo, WasIdempotencyReplay: false);
    }

    /// <summary>
    /// Determine the wallet's tenant scope at append time. <c>WalletOpened</c> events carry their
    /// own <c>TenantId</c>; subsequent events look it up from the wallet's first event in the
    /// table. The lookup is cheap because <c>wallet_events</c> is uniquely indexed on
    /// <c>(wallet_id, sequence_no)</c>.
    /// </summary>
    private async Task<Guid?> ResolveTenantIdAsync(IWalletEvent @event, CancellationToken ct)
    {
        if (@event is WalletOpened opened)
        {
            return opened.TenantId.TryGetValue(out var t) ? t : null;
        }

        return await _db.WalletEvents
            .AsNoTracking()
            .Where(x => x.WalletId == @event.WalletId.Value && x.SequenceNo == 1)
            .Select(x => x.TenantId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}
