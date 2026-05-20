namespace PolarSharp.PrepaidWallets.Abstractions.Commands;

/// <summary>
/// Outcome of a wallet command — either the appended event + new aggregate version, or a typed
/// domain error.
/// </summary>
/// <param name="Event">The event the command produced.</param>
/// <param name="ResultingVersion">The aggregate's new version (sequence number of the last applied event).</param>
public sealed record CommandSuccess(
    Events.IWalletEvent Event,
    long ResultingVersion);

/// <summary>
/// Typed error from a wallet command. Distinct from <see cref="WalletException"/>: errors here
/// are expected domain outcomes (insufficient funds, frozen wallet, debit on closed wallet).
/// </summary>
public abstract record CommandError(string Code, string Message)
{
    /// <summary>The wallet did not exist when the command was applied.</summary>
    /// <param name="WalletId">The wallet that was missing.</param>
    public sealed record WalletNotFound(WalletId WalletId)
        : CommandError("wallet.not_found", $"Wallet '{WalletId}' was not found.");

    /// <summary>An <c>OpenWalletCommand</c> targeted a wallet that already exists.</summary>
    /// <param name="WalletId">The wallet that already existed.</param>
    public sealed record WalletAlreadyExists(WalletId WalletId)
        : CommandError("wallet.already_exists", $"Wallet '{WalletId}' has already been opened.");

    /// <summary>A debit was attempted against a wallet whose balance is below the requested amount.</summary>
    /// <param name="Balance">The wallet's current balance.</param>
    /// <param name="Requested">The amount the debit asked for.</param>
    public sealed record InsufficientFunds(TokenAmount Balance, TokenAmount Requested)
        : CommandError(
            "wallet.insufficient_funds",
            $"Wallet balance {Balance.Value} is below requested debit of {Requested.Value}.");

    /// <summary>A debit or funding command was attempted on a frozen wallet.</summary>
    public sealed record WalletIsFrozen()
        : CommandError("wallet.frozen", "Wallet is frozen; the requested command is rejected.");

    /// <summary>Any command was attempted on a closed wallet.</summary>
    public sealed record WalletIsClosed()
        : CommandError("wallet.closed", "Wallet is closed; no further commands are accepted.");

    /// <summary>A freeze command was attempted on an already-frozen wallet.</summary>
    public sealed record WalletAlreadyFrozen()
        : CommandError("wallet.already_frozen", "Wallet is already frozen.");

    /// <summary>An unfreeze command was attempted on an active wallet.</summary>
    public sealed record WalletNotFrozen()
        : CommandError("wallet.not_frozen", "Wallet is not frozen; unfreeze is a no-op.");

    /// <summary>A validation failure on the command payload itself (e.g. invalid currency).</summary>
    /// <param name="Detail">Free-form validation detail.</param>
    public sealed record ValidationFailed(string Detail)
        : CommandError("wallet.validation", $"Command failed validation: {Detail}");
}
