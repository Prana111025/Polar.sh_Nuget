using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

namespace PolarSharp.PrepaidWallets.Serialization;

/// <summary>
/// AOT-safe JSON event serializer built on the <see cref="WalletJsonContext"/> source-generated
/// metadata. Routes serialization based on the runtime event type; routes deserialization based
/// on the event-type discriminator stored alongside the payload.
/// </summary>
public sealed class JsonWalletEventSerializer : IWalletEventSerializer
{
    private readonly JsonSerializerOptions _options;
    private readonly WalletJsonContext _context;

    /// <summary>Construct the serializer.</summary>
    public JsonWalletEventSerializer()
    {
        _options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = WalletJsonContext.Default,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        };
        _context = WalletJsonContext.Default;
    }

    /// <inheritdoc/>
    public SerializedEvent Serialize(IWalletEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var (json, type) = @event switch
        {
            WalletOpened opened => (JsonSerializer.Serialize(opened, _context.WalletOpened), opened.EventType),
            WalletFunded funded => (JsonSerializer.Serialize(funded, _context.WalletFunded), funded.EventType),
            WalletDebited debited => (JsonSerializer.Serialize(debited, _context.WalletDebited), debited.EventType),
            WalletCredited credited => (JsonSerializer.Serialize(credited, _context.WalletCredited), credited.EventType),
            WalletRefunded refunded => (JsonSerializer.Serialize(refunded, _context.WalletRefunded), refunded.EventType),
            WalletFrozen frozen => (JsonSerializer.Serialize(frozen, _context.WalletFrozen), frozen.EventType),
            WalletUnfrozen unfrozen => (JsonSerializer.Serialize(unfrozen, _context.WalletUnfrozen), unfrozen.EventType),
            WalletClosed closed => (JsonSerializer.Serialize(closed, _context.WalletClosed), closed.EventType),
            _ => throw new UnknownWalletEventTypeException(@event.EventType),
        };

        return new SerializedEvent(type, json);
    }

    /// <inheritdoc/>
    public IWalletEvent Deserialize(string eventType, string payloadJson)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        ArgumentException.ThrowIfNullOrEmpty(payloadJson);

        IWalletEvent? @event = eventType switch
        {
            "wallet.opened" => JsonSerializer.Deserialize(payloadJson, _context.WalletOpened),
            "wallet.funded" => JsonSerializer.Deserialize(payloadJson, _context.WalletFunded),
            "wallet.debited" => JsonSerializer.Deserialize(payloadJson, _context.WalletDebited),
            "wallet.credited" => JsonSerializer.Deserialize(payloadJson, _context.WalletCredited),
            "wallet.refunded" => JsonSerializer.Deserialize(payloadJson, _context.WalletRefunded),
            "wallet.frozen" => JsonSerializer.Deserialize(payloadJson, _context.WalletFrozen),
            "wallet.unfrozen" => JsonSerializer.Deserialize(payloadJson, _context.WalletUnfrozen),
            "wallet.closed" => JsonSerializer.Deserialize(payloadJson, _context.WalletClosed),
            _ => throw new UnknownWalletEventTypeException(eventType),
        };

        return @event ?? throw new UnknownWalletEventTypeException(eventType);
    }

    /// <summary>Expose the configured options for advanced scenarios.</summary>
    public JsonSerializerOptions Options => _options;

    /// <summary>Expose the source-gen type-info resolver.</summary>
    public IJsonTypeInfoResolver TypeInfoResolver => _context;
}
