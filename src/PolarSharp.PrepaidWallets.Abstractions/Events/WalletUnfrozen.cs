namespace PolarSharp.PrepaidWallets.Abstractions.Events;

/// <summary>Re-activates a previously frozen wallet.</summary>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="SequenceNo">Stream sequence number.</param>
/// <param name="OccurredAt">UTC timestamp.</param>
/// <param name="ActorUserId">The operator who lifted the freeze.</param>
/// <param name="IdempotencyKey">The originating command's idempotency key.</param>
public sealed record WalletUnfrozen(
    WalletId WalletId,
    long SequenceNo,
    DateTimeOffset OccurredAt,
    Guid ActorUserId,
    IdempotencyKey IdempotencyKey) : IWalletEvent
{
    /// <inheritdoc/>
    public string EventType => "wallet.unfrozen";
}
