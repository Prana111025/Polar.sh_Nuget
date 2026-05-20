using System.Text.Json.Serialization;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Events;

namespace PolarSharp.PrepaidWallets.Serialization;

/// <summary>
/// System.Text.Json source-generated context for wallet events. Source generation is the
/// AOT-safe choice — reflection-based serialization is disallowed by the package's trim contract
/// (the csproj sets <c>IsAotCompatible</c> and <c>IsTrimmable</c>).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false,
    Converters = [
        typeof(OptionGuidJsonConverter),
        typeof(OptionStringJsonConverter),
        typeof(IdempotencyKeyJsonConverter),
        typeof(WalletIdJsonConverter),
        typeof(TokenAmountJsonConverter),
    ])]
[JsonSerializable(typeof(WalletOpened))]
[JsonSerializable(typeof(WalletFunded))]
[JsonSerializable(typeof(WalletDebited))]
[JsonSerializable(typeof(WalletCredited))]
[JsonSerializable(typeof(WalletRefunded))]
[JsonSerializable(typeof(WalletFrozen))]
[JsonSerializable(typeof(WalletUnfrozen))]
[JsonSerializable(typeof(WalletClosed))]
[JsonSerializable(typeof(FundingSource))]
[JsonSerializable(typeof(WalletId))]
[JsonSerializable(typeof(TokenAmount))]
[JsonSerializable(typeof(IdempotencyKey))]
[JsonSerializable(typeof(Option<Guid>))]
[JsonSerializable(typeof(Option<string>))]
public sealed partial class WalletJsonContext : JsonSerializerContext;
