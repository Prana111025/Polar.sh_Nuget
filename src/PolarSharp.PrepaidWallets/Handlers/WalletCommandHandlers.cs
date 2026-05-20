using MediatR;
using Microsoft.Extensions.Logging;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Commands;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.Domain;

namespace PolarSharp.PrepaidWallets.Handlers;

/// <summary>
/// MediatR handler for every wallet command. The implementation is generic in shape — the
/// per-command branch is a single virtual dispatch over the command type — to keep the
/// MediatR registration surface small and the per-handler ceremony out of the way.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public abstract class WalletCommandHandler<TCommand> : IRequestHandler<TCommand, WalletCommandResult>
    where TCommand : IWalletCommand<WalletCommandResult>
{
    private readonly WalletAggregateLoader _loader;
    private readonly IWalletEventStore _events;
    private readonly IWalletSnapshotStore _snapshots;
    private readonly ISnapshotPolicy _snapshotPolicy;
    private readonly ISystemClock _clock;
    private readonly ILogger _log;

    /// <summary>Construct the handler.</summary>
    /// <param name="loader">Aggregate loader.</param>
    /// <param name="events">Event store.</param>
    /// <param name="snapshots">Snapshot store.</param>
    /// <param name="snapshotPolicy">Snapshot policy.</param>
    /// <param name="clock">Clock used to stamp the event.</param>
    /// <param name="log">Logger for structured logging.</param>
    protected WalletCommandHandler(
        WalletAggregateLoader loader,
        IWalletEventStore events,
        IWalletSnapshotStore snapshots,
        ISnapshotPolicy snapshotPolicy,
        ISystemClock clock,
        ILogger log)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(snapshotPolicy);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(log);
        _loader = loader;
        _events = events;
        _snapshots = snapshots;
        _snapshotPolicy = snapshotPolicy;
        _clock = clock;
        _log = log;
    }

    /// <inheritdoc/>
    public async Task<WalletCommandResult> Handle(TCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var walletOpt = await _loader.LoadAsync(request.WalletId, cancellationToken).ConfigureAwait(false);
        var wallet = walletOpt.GetValueOrDefault(new Wallet());

        // Idempotency short-circuit: if the wallet already saw this key, return the recorded event.
        if (wallet.HasSeen(request.IdempotencyKey))
        {
            var history = await _events
                .LoadAsync(request.WalletId, fromSequenceNoInclusive: 1, cancellationToken)
                .ConfigureAwait(false);
            var existing = history.First(e => e.IdempotencyKey == request.IdempotencyKey);
            return new WalletCommandResult(
                Result<CommandSuccess, CommandError>.Success(new CommandSuccess(existing, existing.SequenceNo)));
        }

        var now = _clock.UtcNow;
        var attempt = TryApply(wallet, request, now);
        if (attempt.IsFailure)
        {
            return new WalletCommandResult(Result<CommandSuccess, CommandError>.Failure(attempt.Error));
        }

        var newEvent = attempt.Value;
        var append = await _events
            .AppendAsync(newEvent, expectedCurrentVersion: wallet.Version, cancellationToken)
            .ConfigureAwait(false);

        if (!append.WasIdempotencyReplay)
        {
            wallet.Apply(append.Event);
            var snapshotOpt = await _snapshots.LoadLatestAsync(request.WalletId, cancellationToken).ConfigureAwait(false);
            var snapshotVersion = snapshotOpt.TryGetValue(out var sn) ? sn.Version : 0L;
            if (_snapshotPolicy.ShouldSnapshot(wallet.Version, snapshotVersion))
            {
                await _snapshots.SaveAsync(wallet.ToSnapshot(now), cancellationToken).ConfigureAwait(false);
                LogTookSnapshot(_log, wallet.Id.Value, wallet.Version, null);
            }
        }

        return new WalletCommandResult(
            Result<CommandSuccess, CommandError>.Success(
                new CommandSuccess(append.Event, append.ResultingVersion)));
    }

    /// <summary>Per-command dispatch — subclasses route to the matching <c>Wallet.Try…</c> method.</summary>
    /// <param name="wallet">The current aggregate.</param>
    /// <param name="command">The command.</param>
    /// <param name="occurredAt">Event timestamp.</param>
    /// <returns>The proposed event or a typed domain error.</returns>
    protected abstract Result<IWalletEvent, CommandError> TryApply(
        Wallet wallet,
        TCommand command,
        DateTimeOffset occurredAt);

    private static readonly Action<ILogger, Guid, long, Exception?> LogTookSnapshot =
        LoggerMessage.Define<Guid, long>(
            LogLevel.Debug,
            new EventId(2001, nameof(LogTookSnapshot)),
            "Wallet {WalletId} snapshotted at version {Version}");
}

/// <summary>Handler for <see cref="OpenWalletCommand"/>.</summary>
public sealed class OpenWalletCommandHandler : WalletCommandHandler<OpenWalletCommand>
{
    /// <summary>Construct the handler.</summary>
    /// <param name="loader">Aggregate loader.</param>
    /// <param name="events">Event store.</param>
    /// <param name="snapshots">Snapshot store.</param>
    /// <param name="snapshotPolicy">Snapshot policy.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="log">Logger.</param>
    public OpenWalletCommandHandler(
        WalletAggregateLoader loader,
        IWalletEventStore events,
        IWalletSnapshotStore snapshots,
        ISnapshotPolicy snapshotPolicy,
        ISystemClock clock,
        ILogger<OpenWalletCommandHandler> log)
        : base(loader, events, snapshots, snapshotPolicy, clock, log) { }

    /// <inheritdoc/>
    protected override Result<IWalletEvent, CommandError> TryApply(
        Wallet wallet,
        OpenWalletCommand command,
        DateTimeOffset occurredAt) =>
        wallet.TryOpen(command, occurredAt);
}

/// <summary>Handler for <see cref="FundWalletCommand"/>.</summary>
public sealed class FundWalletCommandHandler : WalletCommandHandler<FundWalletCommand>
{
    /// <summary>Construct the handler.</summary>
    /// <param name="loader">Aggregate loader.</param>
    /// <param name="events">Event store.</param>
    /// <param name="snapshots">Snapshot store.</param>
    /// <param name="snapshotPolicy">Snapshot policy.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="log">Logger.</param>
    public FundWalletCommandHandler(
        WalletAggregateLoader loader,
        IWalletEventStore events,
        IWalletSnapshotStore snapshots,
        ISnapshotPolicy snapshotPolicy,
        ISystemClock clock,
        ILogger<FundWalletCommandHandler> log)
        : base(loader, events, snapshots, snapshotPolicy, clock, log) { }

    /// <inheritdoc/>
    protected override Result<IWalletEvent, CommandError> TryApply(
        Wallet wallet,
        FundWalletCommand command,
        DateTimeOffset occurredAt) =>
        wallet.TryFund(command, occurredAt);
}

/// <summary>Handler for <see cref="DebitWalletCommand"/>.</summary>
public sealed class DebitWalletCommandHandler : WalletCommandHandler<DebitWalletCommand>
{
    /// <summary>Construct the handler.</summary>
    /// <param name="loader">Aggregate loader.</param>
    /// <param name="events">Event store.</param>
    /// <param name="snapshots">Snapshot store.</param>
    /// <param name="snapshotPolicy">Snapshot policy.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="log">Logger.</param>
    public DebitWalletCommandHandler(
        WalletAggregateLoader loader,
        IWalletEventStore events,
        IWalletSnapshotStore snapshots,
        ISnapshotPolicy snapshotPolicy,
        ISystemClock clock,
        ILogger<DebitWalletCommandHandler> log)
        : base(loader, events, snapshots, snapshotPolicy, clock, log) { }

    /// <inheritdoc/>
    protected override Result<IWalletEvent, CommandError> TryApply(
        Wallet wallet,
        DebitWalletCommand command,
        DateTimeOffset occurredAt) =>
        wallet.TryDebit(command, occurredAt);
}

/// <summary>Handler for <see cref="CreditWalletCommand"/>.</summary>
public sealed class CreditWalletCommandHandler : WalletCommandHandler<CreditWalletCommand>
{
    /// <summary>Construct the handler.</summary>
    /// <param name="loader">Aggregate loader.</param>
    /// <param name="events">Event store.</param>
    /// <param name="snapshots">Snapshot store.</param>
    /// <param name="snapshotPolicy">Snapshot policy.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="log">Logger.</param>
    public CreditWalletCommandHandler(
        WalletAggregateLoader loader,
        IWalletEventStore events,
        IWalletSnapshotStore snapshots,
        ISnapshotPolicy snapshotPolicy,
        ISystemClock clock,
        ILogger<CreditWalletCommandHandler> log)
        : base(loader, events, snapshots, snapshotPolicy, clock, log) { }

    /// <inheritdoc/>
    protected override Result<IWalletEvent, CommandError> TryApply(
        Wallet wallet,
        CreditWalletCommand command,
        DateTimeOffset occurredAt) =>
        wallet.TryCredit(command, occurredAt);
}

/// <summary>Handler for <see cref="RefundWalletCommand"/>.</summary>
public sealed class RefundWalletCommandHandler : WalletCommandHandler<RefundWalletCommand>
{
    /// <summary>Construct the handler.</summary>
    /// <param name="loader">Aggregate loader.</param>
    /// <param name="events">Event store.</param>
    /// <param name="snapshots">Snapshot store.</param>
    /// <param name="snapshotPolicy">Snapshot policy.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="log">Logger.</param>
    public RefundWalletCommandHandler(
        WalletAggregateLoader loader,
        IWalletEventStore events,
        IWalletSnapshotStore snapshots,
        ISnapshotPolicy snapshotPolicy,
        ISystemClock clock,
        ILogger<RefundWalletCommandHandler> log)
        : base(loader, events, snapshots, snapshotPolicy, clock, log) { }

    /// <inheritdoc/>
    protected override Result<IWalletEvent, CommandError> TryApply(
        Wallet wallet,
        RefundWalletCommand command,
        DateTimeOffset occurredAt) =>
        wallet.TryRefund(command, occurredAt);
}

/// <summary>Handler for <see cref="FreezeWalletCommand"/>.</summary>
public sealed class FreezeWalletCommandHandler : WalletCommandHandler<FreezeWalletCommand>
{
    /// <summary>Construct the handler.</summary>
    /// <param name="loader">Aggregate loader.</param>
    /// <param name="events">Event store.</param>
    /// <param name="snapshots">Snapshot store.</param>
    /// <param name="snapshotPolicy">Snapshot policy.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="log">Logger.</param>
    public FreezeWalletCommandHandler(
        WalletAggregateLoader loader,
        IWalletEventStore events,
        IWalletSnapshotStore snapshots,
        ISnapshotPolicy snapshotPolicy,
        ISystemClock clock,
        ILogger<FreezeWalletCommandHandler> log)
        : base(loader, events, snapshots, snapshotPolicy, clock, log) { }

    /// <inheritdoc/>
    protected override Result<IWalletEvent, CommandError> TryApply(
        Wallet wallet,
        FreezeWalletCommand command,
        DateTimeOffset occurredAt) =>
        wallet.TryFreeze(command, occurredAt);
}

/// <summary>Handler for <see cref="UnfreezeWalletCommand"/>.</summary>
public sealed class UnfreezeWalletCommandHandler : WalletCommandHandler<UnfreezeWalletCommand>
{
    /// <summary>Construct the handler.</summary>
    /// <param name="loader">Aggregate loader.</param>
    /// <param name="events">Event store.</param>
    /// <param name="snapshots">Snapshot store.</param>
    /// <param name="snapshotPolicy">Snapshot policy.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="log">Logger.</param>
    public UnfreezeWalletCommandHandler(
        WalletAggregateLoader loader,
        IWalletEventStore events,
        IWalletSnapshotStore snapshots,
        ISnapshotPolicy snapshotPolicy,
        ISystemClock clock,
        ILogger<UnfreezeWalletCommandHandler> log)
        : base(loader, events, snapshots, snapshotPolicy, clock, log) { }

    /// <inheritdoc/>
    protected override Result<IWalletEvent, CommandError> TryApply(
        Wallet wallet,
        UnfreezeWalletCommand command,
        DateTimeOffset occurredAt) =>
        wallet.TryUnfreeze(command, occurredAt);
}

/// <summary>Handler for <see cref="CloseWalletCommand"/>.</summary>
public sealed class CloseWalletCommandHandler : WalletCommandHandler<CloseWalletCommand>
{
    /// <summary>Construct the handler.</summary>
    /// <param name="loader">Aggregate loader.</param>
    /// <param name="events">Event store.</param>
    /// <param name="snapshots">Snapshot store.</param>
    /// <param name="snapshotPolicy">Snapshot policy.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="log">Logger.</param>
    public CloseWalletCommandHandler(
        WalletAggregateLoader loader,
        IWalletEventStore events,
        IWalletSnapshotStore snapshots,
        ISnapshotPolicy snapshotPolicy,
        ISystemClock clock,
        ILogger<CloseWalletCommandHandler> log)
        : base(loader, events, snapshots, snapshotPolicy, clock, log) { }

    /// <inheritdoc/>
    protected override Result<IWalletEvent, CommandError> TryApply(
        Wallet wallet,
        CloseWalletCommand command,
        DateTimeOffset occurredAt) =>
        wallet.TryClose(command, occurredAt);
}
