namespace PolarSharp.PrepaidWallets.Abstractions.Events;

/// <summary>
/// First event in every wallet's stream. Records who owns the wallet, optional tenant scope, and
/// the wallet's currency (locked at open time per the current design — multi-currency is an open
/// question in Case Study 02).
/// </summary>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="SequenceNo">Stream sequence number (always 1 for this event).</param>
/// <param name="OccurredAt">UTC timestamp.</param>
/// <param name="ActorUserId">The operator who opened the wallet.</param>
/// <param name="IdempotencyKey">The originating command's idempotency key.</param>
/// <param name="CustomerId">The customer the wallet belongs to.</param>
/// <param name="TenantId">The tenant scope. <see cref="Option{T}.None"/> in single-tenant deployments.</param>
/// <param name="Currency">ISO-4217 currency code (e.g. "USD"). Locked for the lifetime of the wallet.</param>
public sealed record WalletOpened(
    WalletId WalletId,
    long SequenceNo,
    DateTimeOffset OccurredAt,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey,
    Guid CustomerId,
    Option<Guid> TenantId,
    string Currency) : IWalletEvent
{
    /// <inheritdoc/>
    public string EventType => "wallet.opened";
}
