namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>
/// Strongly-typed identifier for a wallet. Wrapping a <see cref="Guid"/> in this record prevents
/// callers from accidentally passing a <see cref="Guid"/> meant to identify (for example) a
/// customer or a tenant to a wallet API.
/// </summary>
/// <param name="Value">The underlying <see cref="Guid"/>.</param>
/// <example>
/// <code>
/// var walletId = WalletId.NewId();
/// var command = new OpenWalletCommand(walletId, customerId: Guid.NewGuid(), tenantId: Option&lt;Guid&gt;.None, currency: "USD", idempotencyKey: IdempotencyKey.Create("open-1"));
/// </code>
/// </example>
public readonly record struct WalletId(Guid Value)
{
    /// <summary>Generate a new random wallet id.</summary>
    /// <returns>A fresh <see cref="WalletId"/> wrapping <see cref="Guid.NewGuid"/>.</returns>
    public static WalletId NewId() => new(Guid.NewGuid());

    /// <summary>The sentinel "no wallet" identifier.</summary>
    public static WalletId Empty { get; } = new(Guid.Empty);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D");
}
