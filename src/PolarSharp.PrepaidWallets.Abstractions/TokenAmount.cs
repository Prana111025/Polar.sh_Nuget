namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>
/// Non-negative integer count of wallet tokens. Wraps a <see cref="long"/> in a value-object so the
/// type system rules out negative amounts at compile-time-adjacent (construction-time) checks,
/// rather than relying on every consumer remembering to validate.
/// </summary>
/// <remarks>
/// Tokens are integral by design — fractional tokens add modeling complexity without economic
/// benefit. Hosts that want fractional pricing express it through the funding-event's customer
/// charge amount (in cents) and the per-tenant tokens-to-currency ratio captured in the
/// <c>FundingTermsSnapshotJson</c>.
/// </remarks>
public readonly record struct TokenAmount : IComparable<TokenAmount>
{
    /// <summary>The wrapped token count.</summary>
    public long Value { get; }

    /// <summary>Construct a token amount.</summary>
    /// <param name="value">A non-negative token count.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is negative.</exception>
    public TokenAmount(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Token amount must be non-negative.");
        }

        Value = value;
    }

    /// <summary>The zero token amount.</summary>
    public static TokenAmount Zero { get; } = new(0);

    /// <summary>Sum two token amounts.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>The sum, validated as non-negative.</returns>
    /// <exception cref="OverflowException">Thrown when the sum overflows <see cref="long"/>.</exception>
    public static TokenAmount operator +(TokenAmount left, TokenAmount right) =>
        new(checked(left.Value + right.Value));

    /// <summary>Subtract one token amount from another.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the result would be negative.</exception>
    public static TokenAmount operator -(TokenAmount left, TokenAmount right) =>
        new(left.Value - right.Value);

    /// <summary>Strict-less-than comparison.</summary>
    public static bool operator <(TokenAmount left, TokenAmount right) => left.Value < right.Value;

    /// <summary>Strict-greater-than comparison.</summary>
    public static bool operator >(TokenAmount left, TokenAmount right) => left.Value > right.Value;

    /// <summary>Less-or-equal comparison.</summary>
    public static bool operator <=(TokenAmount left, TokenAmount right) => left.Value <= right.Value;

    /// <summary>Greater-or-equal comparison.</summary>
    public static bool operator >=(TokenAmount left, TokenAmount right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public int CompareTo(TokenAmount other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
