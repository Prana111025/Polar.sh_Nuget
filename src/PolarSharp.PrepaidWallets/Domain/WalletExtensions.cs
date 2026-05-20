using PolarSharp.PrepaidWallets.Abstractions;
using PolarSharp.PrepaidWallets.Abstractions.Queries;
using PolarSharp.PrepaidWallets.Abstractions.Stores;

namespace PolarSharp.PrepaidWallets.Domain;

/// <summary>Helpers that convert between the aggregate and the read-model / snapshot types.</summary>
public static class WalletExtensions
{
    /// <summary>Take a snapshot at the wallet's current version.</summary>
    /// <param name="wallet">The aggregate.</param>
    /// <param name="takenAt">UTC timestamp the snapshot is being taken at.</param>
    /// <returns>A <see cref="WalletSnapshot"/> reflecting <paramref name="wallet"/>'s current state.</returns>
    public static WalletSnapshot ToSnapshot(this Wallet wallet, DateTimeOffset takenAt)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        return new WalletSnapshot(
            wallet.Id,
            wallet.Version,
            wallet.CustomerId,
            wallet.TenantId,
            wallet.Currency,
            wallet.Balance,
            wallet.Status,
            wallet.OpenedAt,
            wallet.LastActivityAt,
            takenAt);
    }

    /// <summary>Project the aggregate to the read-model view used by queries.</summary>
    /// <param name="wallet">The aggregate.</param>
    /// <returns>A <see cref="WalletStateView"/> reflecting the aggregate.</returns>
    public static WalletStateView ToStateView(this Wallet wallet)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        return new WalletStateView(
            wallet.Id,
            wallet.CustomerId,
            wallet.TenantId,
            wallet.Currency,
            wallet.Balance,
            wallet.Status,
            wallet.Version,
            wallet.OpenedAt,
            wallet.LastActivityAt);
    }
}
