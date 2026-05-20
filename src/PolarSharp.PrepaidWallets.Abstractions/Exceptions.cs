namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>Base type for all wallet-feature exceptions.</summary>
/// <remarks>
/// Per the project's coding standards, exceptions express programmer errors or exceptional system
/// failures. Expected domain failure modes (insufficient funds, frozen wallet, idempotency replay)
/// are returned as <see cref="Result{T, TError}"/> values from command handlers; exceptions here
/// signal "you broke the contract" or "infrastructure broke".
/// </remarks>
public abstract class WalletException : Exception
{
    /// <summary>Construct a wallet exception with a message.</summary>
    /// <param name="message">The error message.</param>
    protected WalletException(string message)
        : base(message) { }

    /// <summary>Construct a wallet exception with a message + inner cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    protected WalletException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>Thrown when a query/command targets a wallet that does not exist.</summary>
public sealed class WalletNotFoundException : WalletException
{
    /// <summary>Construct an exception for the missing wallet.</summary>
    /// <param name="walletId">The wallet id that could not be found.</param>
    public WalletNotFoundException(WalletId walletId)
        : base($"Wallet '{walletId}' was not found.") =>
        WalletId = walletId;

    /// <summary>The wallet id that could not be found.</summary>
    public WalletId WalletId { get; }
}

/// <summary>
/// Thrown by the event store when an append races with another writer and the expected sequence
/// number is no longer current. MediatR retry behavior recovers — the failing command is loaded
/// against the new state and re-applied.
/// </summary>
public sealed class WalletConcurrencyConflictException : WalletException
{
    /// <summary>Construct a concurrency-conflict exception.</summary>
    /// <param name="walletId">The wallet under contention.</param>
    /// <param name="expectedSequenceNo">The sequence number the writer expected.</param>
    /// <param name="actualSequenceNo">The sequence number found at append time.</param>
    public WalletConcurrencyConflictException(WalletId walletId, long expectedSequenceNo, long actualSequenceNo)
        : base(
            $"Optimistic-concurrency conflict on wallet '{walletId}': expected sequence {expectedSequenceNo}, "
                + $"but the store now reports sequence {actualSequenceNo}.")
    {
        WalletId = walletId;
        ExpectedSequenceNo = expectedSequenceNo;
        ActualSequenceNo = actualSequenceNo;
    }

    /// <summary>The wallet under contention.</summary>
    public WalletId WalletId { get; }

    /// <summary>The sequence number the writer expected.</summary>
    public long ExpectedSequenceNo { get; }

    /// <summary>The sequence number actually present at append time.</summary>
    public long ActualSequenceNo { get; }
}

/// <summary>
/// Thrown when a previously-recorded idempotency key is re-used with a different command payload.
/// Replay of the same payload returns the original outcome; payload divergence is a programmer
/// error and surfaces as this exception.
/// </summary>
public sealed class IdempotencyKeyMismatchException : WalletException
{
    /// <summary>Construct an idempotency-mismatch exception.</summary>
    /// <param name="walletId">The wallet the key was recorded against.</param>
    /// <param name="key">The replayed idempotency key.</param>
    public IdempotencyKeyMismatchException(WalletId walletId, IdempotencyKey key)
        : base(
            $"Idempotency key '{key}' on wallet '{walletId}' was previously used by a different command payload. "
                + "Replays must carry the same payload as the original.")
    {
        WalletId = walletId;
        Key = key;
    }

    /// <summary>The wallet the key was recorded against.</summary>
    public WalletId WalletId { get; }

    /// <summary>The replayed idempotency key.</summary>
    public IdempotencyKey Key { get; }
}

/// <summary>Thrown when an event-store payload cannot be deserialized to a known event type.</summary>
public sealed class UnknownWalletEventTypeException : WalletException
{
    /// <summary>Construct an unknown-event-type exception.</summary>
    /// <param name="eventType">The event-type discriminator that could not be resolved.</param>
    public UnknownWalletEventTypeException(string eventType)
        : base($"Wallet event type '{eventType}' is not registered with the event serializer.") =>
        EventType = eventType;

    /// <summary>The event-type discriminator that could not be resolved.</summary>
    public string EventType { get; }
}
