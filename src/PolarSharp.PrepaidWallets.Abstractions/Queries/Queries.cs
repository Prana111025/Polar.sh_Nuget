using MediatR;
using PolarSharp.PrepaidWallets.Abstractions.Events;

namespace PolarSharp.PrepaidWallets.Abstractions.Queries;

/// <summary>Current denormalized state of a wallet — folded from its event stream.</summary>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="CustomerId">Customer who owns the wallet.</param>
/// <param name="TenantId">Tenant scope. <see cref="Option{T}.None"/> in single-tenant deployments.</param>
/// <param name="Currency">ISO-4217 currency code.</param>
/// <param name="Balance">Current token balance.</param>
/// <param name="Status">Lifecycle status.</param>
/// <param name="Version">Sequence number of the last applied event.</param>
/// <param name="OpenedAt">UTC timestamp of <see cref="WalletOpened"/>.</param>
/// <param name="LastActivityAt">UTC timestamp of the last appended event.</param>
public sealed record WalletStateView(
    WalletId WalletId,
    Guid CustomerId,
    Option<Guid> TenantId,
    string Currency,
    TokenAmount Balance,
    WalletStatus Status,
    long Version,
    DateTimeOffset OpenedAt,
    DateTimeOffset LastActivityAt);

/// <summary>Fetch the current state of a wallet by id.</summary>
/// <param name="WalletId">The wallet identifier.</param>
public sealed record GetWalletStateQuery(WalletId WalletId)
    : IRequest<Option<WalletStateView>>;

/// <summary>Fetch the current token balance only (lighter than full state when that's all the caller needs).</summary>
/// <param name="WalletId">The wallet identifier.</param>
public sealed record GetWalletBalanceQuery(WalletId WalletId)
    : IRequest<Option<TokenAmount>>;

/// <summary>Fetch a page of events from a wallet's stream.</summary>
/// <param name="WalletId">The wallet identifier.</param>
/// <param name="FromSequenceNoInclusive">First sequence number to include (1-based).</param>
/// <param name="MaxEvents">Maximum events to return (capped server-side at 500).</param>
public sealed record GetWalletHistoryQuery(
    WalletId WalletId,
    long FromSequenceNoInclusive,
    int MaxEvents) : IRequest<IReadOnlyList<IWalletEvent>>;
