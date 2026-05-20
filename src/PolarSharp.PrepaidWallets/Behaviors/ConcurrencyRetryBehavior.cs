using MediatR;
using Microsoft.Extensions.Logging;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Commands;

namespace PolarSharp.PrepaidWallets.Behaviors;

/// <summary>
/// MediatR behavior that re-runs a command up to a small number of times when an event-store
/// optimistic-concurrency conflict surfaces. Per Case Study 02 step 2 ("Optimistic concurrency
/// via unique (wallet_id, sequence_no) index — second writer on a stale read fails; MediatR retry
/// behavior recovers").
/// </summary>
/// <typeparam name="TRequest">The command type.</typeparam>
public sealed class ConcurrencyRetryBehavior<TRequest>
    : IPipelineBehavior<TRequest, WalletCommandResult>
    where TRequest : IWalletCommand<WalletCommandResult>
{
    /// <summary>The maximum number of attempts (initial + retries).</summary>
    public const int MaxAttempts = 3;

    private readonly ILogger<ConcurrencyRetryBehavior<TRequest>> _log;

    /// <summary>Construct the behavior.</summary>
    /// <param name="log">Logger.</param>
    public ConcurrencyRetryBehavior(ILogger<ConcurrencyRetryBehavior<TRequest>> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
    }

    /// <inheritdoc/>
    public async Task<WalletCommandResult> Handle(
        TRequest request,
        RequestHandlerDelegate<WalletCommandResult> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var attempts = 0;
        while (true)
        {
            attempts++;
            try
            {
                return await next(cancellationToken).ConfigureAwait(false);
            }
            catch (WalletConcurrencyConflictException) when (attempts < MaxAttempts)
            {
                LogRetrying(_log, request.WalletId.Value, attempts, null);
                // Tight loop — the loader will re-read the latest stream on the next attempt.
            }
        }
    }

    private static readonly Action<ILogger, Guid, int, Exception?> LogRetrying =
        LoggerMessage.Define<Guid, int>(
            LogLevel.Information,
            new EventId(2200, nameof(LogRetrying)),
            "Retrying wallet command on {WalletId} after concurrency conflict (attempt {Attempt})");
}
