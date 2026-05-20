using System.Text.Json;
using System.Text.Json.Serialization;
using PolarSharp.PrepaidWallets.Abstractions;

namespace PolarSharp.PrepaidWallets.Serialization;

/// <summary>JSON converter for <see cref="IdempotencyKey"/> — serialized as a plain JSON string.</summary>
public sealed class IdempotencyKeyJsonConverter : JsonConverter<IdempotencyKey>
{
    /// <inheritdoc/>
    public override IdempotencyKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString()
            ?? throw new JsonException("IdempotencyKey must be a non-null JSON string.");
        return IdempotencyKey.Create(s);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IdempotencyKey value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>JSON converter for <see cref="WalletId"/> — serialized as a plain JSON Guid string.</summary>
public sealed class WalletIdJsonConverter : JsonConverter<WalletId>
{
    /// <inheritdoc/>
    public override WalletId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, WalletId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>JSON converter for <see cref="TokenAmount"/> — serialized as a plain JSON number.</summary>
public sealed class TokenAmountJsonConverter : JsonConverter<TokenAmount>
{
    /// <inheritdoc/>
    public override TokenAmount Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetInt64());

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, TokenAmount value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.Value);
    }
}
