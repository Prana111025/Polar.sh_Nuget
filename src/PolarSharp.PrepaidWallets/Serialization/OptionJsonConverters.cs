using System.Text.Json;
using System.Text.Json.Serialization;
using PolarSharp.PrepaidWallets.Abstractions;

namespace PolarSharp.PrepaidWallets.Serialization;

/// <summary>
/// AOT-safe JSON converter for <see cref="Option{T}"/> of <see cref="Guid"/>. None is serialized as
/// JSON null; Some(g) is serialized as the GUID string. Source generation does not provide a built-in
/// converter for our value-type Option, so this small converter pair is the smallest change that
/// keeps Option a first-class wallet-abstraction citizen.
/// </summary>
public sealed class OptionGuidJsonConverter : JsonConverter<Option<Guid>>
{
    /// <inheritdoc/>
    public override Option<Guid> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return Option<Guid>.None;
        }

        return Option<Guid>.Some(reader.GetGuid());
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Option<Guid> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }
}

/// <summary>AOT-safe JSON converter for <see cref="Option{T}"/> of <see cref="string"/>.</summary>
public sealed class OptionStringJsonConverter : JsonConverter<Option<string>>
{
    /// <inheritdoc/>
    public override Option<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return Option<string>.None;
        }

        var s = reader.GetString();
        return s is null ? Option<string>.None : Option<string>.Some(s);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Option<string> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }
}
