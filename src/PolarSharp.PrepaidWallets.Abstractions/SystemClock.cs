namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>
/// Time-source boundary. Tests substitute a deterministic clock; production uses
/// <see cref="DateTimeOffset.UtcNow"/>. Decoupling time from <c>DateTimeOffset.UtcNow</c> at every
/// site that records "when did this happen" makes event-sourcing tests determinate and replayable.
/// </summary>
public interface ISystemClock
{
    /// <summary>The current UTC instant.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>Production clock backed by <see cref="DateTimeOffset.UtcNow"/>.</summary>
public sealed class SystemClock : ISystemClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
