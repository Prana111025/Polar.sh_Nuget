using MediatR;
using Microsoft.Extensions.Logging;
using PolarSharp.PrepaidWallets.Abstractions.Commands;

namespace PolarSharp.PrepaidWallets.Behaviors;

/// <summary>
/// MediatR behavior that structures a single log line per command — wallet id, command type,
/// idempotency key, outcome. Per Case Study 02's MediatR pipeline ("Logging behavior — structured
/// log; redact PII"), only the discriminator-shaped fields are logged; payloads with cents,
/// snapshots, or PII are not.
/// </summary>
/// <typeparam name="TRequest">The command type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IWalletCommand<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _log;

    /// <summary>Construct the behavior.</summary>
    /// <param name="log">Logger.</param>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var requestTypeName = typeof(TRequest).Name;
        LogStarting(_log, requestTypeName, request.WalletId.Value, request.IdempotencyKey.Value, null);
        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            LogCompleted(_log, requestTypeName, request.WalletId.Value, null);
            return response;
        }
        catch (Exception ex)
        {
            LogFailed(_log, requestTypeName, request.WalletId.Value, ex);
            throw;
        }
    }

    private static readonly Action<ILogger, string, Guid, string, Exception?> LogStarting =
        LoggerMessage.Define<string, Guid, string>(
            LogLevel.Debug,
            new EventId(2100, nameof(LogStarting)),
            "Wallet command {RequestType} starting (wallet {WalletId}, idempotency-key {IdempotencyKey})");

    private static readonly Action<ILogger, string, Guid, Exception?> LogCompleted =
        LoggerMessage.Define<string, Guid>(
            LogLevel.Debug,
            new EventId(2101, nameof(LogCompleted)),
            "Wallet command {RequestType} completed (wallet {WalletId})");

    private static readonly Action<ILogger, string, Guid, Exception?> LogFailed =
        LoggerMessage.Define<string, Guid>(
            LogLevel.Warning,
            new EventId(2102, nameof(LogFailed)),
            "Wallet command {RequestType} failed (wallet {WalletId})");
}
