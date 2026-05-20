namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>
/// Caller-supplied key that makes a wallet command safely retryable. Two commands with the same
/// idempotency key on the same wallet produce the same event exactly once — replays return the
/// original outcome rather than appending a duplicate event.
/// </summary>
/// <remarks>
/// Per Case Study 02 "Event-Sourced Wallet With Comprehensive Economic Modeling", every wallet
/// command requires an idempotency key. Network retries, webhook re-deliveries, partial-failure
/// resumes — all assume idempotency. The wallet enforces it; callers cannot opt out.
/// </remarks>
public readonly record struct IdempotencyKey
{
    /// <summary>The maximum allowed key length in characters.</summary>
    public const int MaxLength = 128;

    /// <summary>The wrapped key.</summary>
    public string Value { get; }

    private IdempotencyKey(string value) => Value = value;

    /// <summary>Build a validated idempotency key.</summary>
    /// <param name="value">A non-empty string of at most <see cref="MaxLength"/> characters.</param>
    /// <returns>A validated <see cref="IdempotencyKey"/>.</returns>
    /// <exception cref="ArgumentException">Thrown for null/empty input or input exceeding <see cref="MaxLength"/>.</exception>
    public static IdempotencyKey Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Idempotency key must be at most {MaxLength} characters.",
                nameof(value));
        }

        return new IdempotencyKey(value);
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
