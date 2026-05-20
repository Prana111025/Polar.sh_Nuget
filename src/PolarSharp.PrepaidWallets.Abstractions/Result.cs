namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>
/// Outcome of a wallet operation — either a typed success value or a typed error. Used to express
/// expected, non-exceptional failure modes (insufficient funds, idempotency key collision, wallet
/// frozen, etc.) without throwing exceptions on hot paths.
/// </summary>
/// <remarks>
/// Per the ZoranHorvat .NET coding standards followed by this codebase: exceptions are for
/// programmer errors and exceptional system failures; <see cref="Result{T, TError}"/> models the
/// expected failure modes of a domain operation. The two are complementary, not substitutes.
/// </remarks>
/// <typeparam name="T">The success-value type.</typeparam>
/// <typeparam name="TError">The error type.</typeparam>
public readonly struct Result<T, TError> : IEquatable<Result<T, TError>>
{
    private readonly T? _value;
    private readonly TError? _error;

    private Result(T? value, TError? error, bool isSuccess)
    {
        _value = value;
        _error = error;
        IsSuccess = isSuccess;
    }

    /// <summary><see langword="true"/> when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary><see langword="true"/> when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The success value.</summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a failed result.</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Result is a failure; read Error instead.");

    /// <summary>The error value.</summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a successful result.</exception>
    public TError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Result is a success; read Value instead.");

    /// <summary>Build a successful result.</summary>
    /// <param name="value">The success value.</param>
    public static Result<T, TError> Success(T value) => new(value, default, isSuccess: true);

    /// <summary>Build a failed result.</summary>
    /// <param name="error">The error value.</param>
    public static Result<T, TError> Failure(TError error) => new(default, error, isSuccess: false);

    /// <summary>Pattern-match over the success and failure cases without throwing.</summary>
    /// <typeparam name="TOut">The match output type.</typeparam>
    /// <param name="onSuccess">Invoked with the success value.</param>
    /// <param name="onFailure">Invoked with the error.</param>
    /// <returns>The result of whichever delegate ran.</returns>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<TError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <inheritdoc/>
    public bool Equals(Result<T, TError> other) =>
        IsSuccess == other.IsSuccess
        && EqualityComparer<T?>.Default.Equals(_value, other._value)
        && EqualityComparer<TError?>.Default.Equals(_error, other._error);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Result<T, TError> r && Equals(r);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(IsSuccess, _value, _error);

    /// <summary>Structural equality.</summary>
    public static bool operator ==(Result<T, TError> left, Result<T, TError> right) => left.Equals(right);

    /// <summary>Structural inequality.</summary>
    public static bool operator !=(Result<T, TError> left, Result<T, TError> right) => !left.Equals(right);
}
