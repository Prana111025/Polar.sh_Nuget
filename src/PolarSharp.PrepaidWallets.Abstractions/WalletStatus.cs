namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>
/// Lifecycle status of a wallet aggregate.
/// </summary>
/// <remarks>
/// Status transitions are linear: <see cref="Active"/> can be frozen (and unfrozen back to
/// <see cref="Active"/>), and any state can be closed terminally. Closed wallets reject all
/// further commands.
/// </remarks>
public enum WalletStatus
{
    /// <summary>The wallet exists and accepts funding, debit, credit, and refund operations.</summary>
    Active = 0,

    /// <summary>The wallet is suspended — debits and funding are rejected; credits + refunds are allowed only via operator action.</summary>
    Frozen = 1,

    /// <summary>The wallet is closed terminally — all subsequent commands are rejected.</summary>
    Closed = 2,
}
