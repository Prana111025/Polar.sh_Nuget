using MediatR;

namespace PolarSharp.PrepaidWallets.Abstractions.Commands;

/// <summary>
/// Marker interface for every wallet command, layered on top of <c>MediatR.IRequest</c>. Every
/// wallet command carries an <see cref="IdempotencyKey"/>; the MediatR pipeline's idempotency
/// behavior short-circuits replays.
/// </summary>
/// <remarks>
/// Per Case Study 02 step 3 ("MediatR pipeline with behaviors"), every command flows through the
/// idempotency / validation / logging / transaction behaviors before reaching the handler. The
/// handler then loads the aggregate, invokes the matching aggregate command, and returns the new
/// events the aggregate produced.
/// </remarks>
/// <typeparam name="TResponse">The handler response type.</typeparam>
public interface IWalletCommand<out TResponse> : IRequest<TResponse>
{
    /// <summary>The wallet this command targets.</summary>
    WalletId WalletId { get; }

    /// <summary>Idempotency key — replays return the original outcome.</summary>
    IdempotencyKey IdempotencyKey { get; }
}
