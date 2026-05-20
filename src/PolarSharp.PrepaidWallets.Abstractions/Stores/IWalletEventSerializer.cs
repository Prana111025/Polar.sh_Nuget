using PolarSharp.PrepaidWallets.Abstractions.Events;

namespace PolarSharp.PrepaidWallets.Abstractions.Stores;

/// <summary>
/// Boundary for converting wallet events to / from the on-wire JSON form used by event stores.
/// Implementations use System.Text.Json source-generated contexts for AOT compatibility — no
/// reflection-based serializers.
/// </summary>
public interface IWalletEventSerializer
{
    /// <summary>Serialize an event to its stable JSON form.</summary>
    /// <param name="event">The event to serialize.</param>
    /// <returns>The JSON payload and the event-type discriminator.</returns>
    SerializedEvent Serialize(IWalletEvent @event);

    /// <summary>Deserialize an event from its on-wire form.</summary>
    /// <param name="eventType">The event-type discriminator (matches <see cref="IWalletEvent.EventType"/>).</param>
    /// <param name="payloadJson">The JSON payload as written by <see cref="Serialize"/>.</param>
    /// <returns>The deserialized event.</returns>
    /// <exception cref="UnknownWalletEventTypeException">Thrown when <paramref name="eventType"/> is not registered.</exception>
    IWalletEvent Deserialize(string eventType, string payloadJson);
}

/// <summary>The on-wire form of a wallet event.</summary>
/// <param name="EventType">The stable event-type discriminator.</param>
/// <param name="PayloadJson">The JSON-serialized event body.</param>
public sealed record SerializedEvent(string EventType, string PayloadJson);
