namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>
/// Discriminated "value-or-nothing" alternative to nullable reference types. Used by the wallet
/// abstractions for optional value-object inputs where <see langword="null"/> is semantically meaningful
/// (e.g. an idempotency key may legitimately be absent rather than "the empty string").
/// </summary>
/// <remarks>
/// <para>
/// Patterns like <c>option.TryGetValue(out var v)</c> or <c>option.GetValueOrDefault(fallback)</c>
/// are preferred over property access so callers cannot accidentally read <see cref="Value"/> on a
/// None instance.
/// </para>
/// <para>
/// <see cref="Option{T}"/> is a readonly struct — AOT-safe, allocation-free, and trim-compatible.
/// </para>
/// </remarks>
/// <typeparam name="T">The wrapped value type.</typeparam>
public readonly struct Option<T> : IEquatable<Option<T>>
{
    private readonly T? _value;

    private Option(T value, bool hasValue)
    {
        _value = value;
        HasValue = hasValue;
    }

    /// <summary><see langword="true"/> when the option carries a value.</summary>
    public bool HasValue { get; }

    /// <summary>The wrapped value. Read only when <see cref="HasValue"/> is <see langword="true"/>.</summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a None instance.</exception>
    public T Value => HasValue
        ? _value!
        : throw new InvalidOperationException("Option has no value (it is None).");

    /// <summary>The empty option.</summary>
    public static Option<T> None => default;

    /// <summary>Construct an option that carries a value.</summary>
    /// <param name="value">The wrapped value (must not be null when <typeparamref name="T"/> is a reference type).</param>
    public static Option<T> Some(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Option<T>(value, hasValue: true);
    }

    /// <summary>Try to read the value without throwing.</summary>
    /// <param name="value">Receives the value when present; <c>default</c> otherwise.</param>
    /// <returns><see langword="true"/> when the option carries a value.</returns>
    public bool TryGetValue(out T value)
    {
        value = _value!;
        return HasValue;
    }

    /// <summary>Read the value or return the supplied fallback.</summary>
    /// <param name="fallback">The value to return when the option is None.</param>
    /// <returns>Either the wrapped value or the fallback.</returns>
    public T GetValueOrDefault(T fallback) => HasValue ? _value! : fallback;

    /// <inheritdoc/>
    public bool Equals(Option<T> other) =>
        HasValue == other.HasValue
        && EqualityComparer<T?>.Default.Equals(_value, other._value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Option<T> o && Equals(o);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HasValue ? HashCode.Combine(true, _value) : 0;

    /// <summary>Structural equality.</summary>
    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);

    /// <summary>Structural inequality.</summary>
    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);
}
