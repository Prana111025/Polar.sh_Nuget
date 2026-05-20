using MediatR;
using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Events;
using PolarSharp.PrepaidWallets.Abstractions.Queries;
using PolarSharp.PrepaidWallets.Abstractions.Stores;
using PolarSharp.PrepaidWallets.Domain;

namespace PolarSharp.PrepaidWallets.Handlers;

/// <summary>Handler for <see cref="GetWalletStateQuery"/>.</summary>
public sealed class GetWalletStateQueryHandler
    : IRequestHandler<GetWalletStateQuery, Option<WalletStateView>>
{
    private readonly WalletAggregateLoader _loader;

    /// <summary>Construct the handler.</summary>
    /// <param name="loader">Aggregate loader.</param>
    public GetWalletStateQueryHandler(WalletAggregateLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _loader = loader;
    }

    /// <inheritdoc/>
    public async Task<Option<WalletStateView>> Handle(GetWalletStateQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var walletOpt = await _loader.LoadAsync(request.WalletId, cancellationToken).ConfigureAwait(false);
        return walletOpt.TryGetValue(out var wallet)
            ? Option<WalletStateView>.Some(wallet.ToStateView())
            : Option<WalletStateView>.None;
    }
}

/// <summary>Handler for <see cref="GetWalletBalanceQuery"/>.</summary>
public sealed class GetWalletBalanceQueryHandler
    : IRequestHandler<GetWalletBalanceQuery, Option<TokenAmount>>
{
    private readonly WalletAggregateLoader _loader;

    /// <summary>Construct the handler.</summary>
    /// <param name="loader">Aggregate loader.</param>
    public GetWalletBalanceQueryHandler(WalletAggregateLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _loader = loader;
    }

    /// <inheritdoc/>
    public async Task<Option<TokenAmount>> Handle(GetWalletBalanceQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var walletOpt = await _loader.LoadAsync(request.WalletId, cancellationToken).ConfigureAwait(false);
        return walletOpt.TryGetValue(out var wallet)
            ? Option<TokenAmount>.Some(wallet.Balance)
            : Option<TokenAmount>.None;
    }
}

/// <summary>Handler for <see cref="GetWalletHistoryQuery"/>.</summary>
public sealed class GetWalletHistoryQueryHandler
    : IRequestHandler<GetWalletHistoryQuery, IReadOnlyList<IWalletEvent>>
{
    private const int HardCapMaxEvents = 500;

    private readonly IWalletEventStore _events;

    /// <summary>Construct the handler.</summary>
    /// <param name="events">Event store.</param>
    public GetWalletHistoryQueryHandler(IWalletEventStore events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _events = events;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IWalletEvent>> Handle(
        GetWalletHistoryQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var from = Math.Max(1L, request.FromSequenceNoInclusive);
        var max = Math.Clamp(request.MaxEvents, 1, HardCapMaxEvents);

        var page = await _events
            .LoadAsync(request.WalletId, from, cancellationToken)
            .ConfigureAwait(false);

        if (page.Count <= max)
        {
            return page;
        }

        var result = new IWalletEvent[max];
        for (var i = 0; i < max; i++)
        {
            result[i] = page[i];
        }

        return result;
    }
}
