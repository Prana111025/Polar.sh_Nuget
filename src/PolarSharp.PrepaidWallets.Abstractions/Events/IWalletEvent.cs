namespace PolarSharp.PrepaidWallets.Abstractions.Events;

/// <summary>
/// Marker interface implemented by every wallet domain event. The wallet ledger's source of truth
/// is the ordered sequence of <see cref="IWalletEvent"/> values; the aggregate state is a fold over
/// that sequence.
/// </summary>
/// <remarks>
/// <para>
/// Per Case Study 02 "Event-Sourced Wallet With Comprehensive Economic Modeling", events are
/// immutable records. Once appended they are never mutated; corrections happen by appending a new
/// compensating event, never by editing history.
/// </para>
/// <para>
/// All events are AOT-safe records with explicit, JSON-source-gen-compatible property types.
/// </para>
/// </remarks>
public interface IWalletEvent
{
    /// <summary>The wallet aggregate the event belongs to.</summary>
    WalletId WalletId { get; }

    /// <summary>Position of this event in the wallet's event stream. 1-based, gap-free, monotonically increasing.</summary>
    long SequenceNo { get; }

    /// <summary>UTC instant the event was recorded.</summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>The actor (user) that caused the event. <see cref="Guid.Empty"/> for system-generated events.</summary>
    Guid ActorUserId { get; }

    /// <summary>The idempotency key the originating command carried. Present on every event written by a command.</summary>
    IdempotencyKey IdempotencyKey { get; }

    /// <summary>Stable event-type discriminator. Used by the event serializer for round-tripping.</summary>
    string EventType { get; }
}
