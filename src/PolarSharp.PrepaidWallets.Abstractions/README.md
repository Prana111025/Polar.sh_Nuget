# PolarSharp.PrepaidWallets.Abstractions

Abstractions for the PolarSharp prepaid-wallet ledger — events, commands, queries, value objects,
exceptions, store interfaces. Lift-shift safe (zero PolarSharp.* deps).

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.Abstractions
```

## Quickstart

```csharp
// Hosts implement IWalletIdentityProvider against their own identity infrastructure:
public sealed class AspNetCoreWalletIdentity(IHttpContextAccessor http) : IWalletIdentityProvider
{
    public Guid CurrentUserId => Guid.Parse(http.HttpContext!.User.FindFirst("sub")!.Value);
    public Guid? CurrentTenantId => null;
    public bool IsMultiTenantMode => false;
}
services.AddScoped<IWalletIdentityProvider, AspNetCoreWalletIdentity>();
```

## What this package contains

- **Value objects**: `WalletId`, `TokenAmount`, `IdempotencyKey`, `FundingSource`, `Option<T>`,
  `Result<T, TError>`.
- **Events** (`PolarSharp.PrepaidWallets.Abstractions.Events`): `WalletOpened`, `WalletFunded`
  (with the full economic-breakdown fields per Case Study 02), `WalletDebited`, `WalletCredited`,
  `WalletRefunded`, `WalletFrozen`, `WalletUnfrozen`, `WalletClosed`. All implement `IWalletEvent`.
- **Commands** (`PolarSharp.PrepaidWallets.Abstractions.Commands`): one record per event kind plus
  `WalletCommandResult` carrying `Result<CommandSuccess, CommandError>`.
- **Queries** (`PolarSharp.PrepaidWallets.Abstractions.Queries`): `GetWalletStateQuery`,
  `GetWalletBalanceQuery`, `GetWalletHistoryQuery`, plus the `WalletStateView` projection record.
- **Store interfaces** (`PolarSharp.PrepaidWallets.Abstractions.Stores`): `IWalletEventStore`,
  `IWalletSnapshotStore`, `IWalletEventSerializer`, `WalletSnapshot`, `SerializedEvent`,
  `AppendOutcome`.
- **Exceptions**: `WalletException` base + `WalletNotFoundException`,
  `WalletConcurrencyConflictException`, `IdempotencyKeyMismatchException`,
  `UnknownWalletEventTypeException`.

**Lift-safe (zero PolarSharp.* deps)** — by design. Per Case Study 01 "Lift-And-Shift
Architecture", every type in this package is independent of any other PolarSharp.* package. The
only third-party deps are `MediatR.Contracts` (`IRequest<T>` for commands and queries).

## Status

v1.3.0 — Phase 20 ships the full abstraction set used by the core, Marten provider, and EF Core
provider. The Polar bridge packages and notification dispatcher continue in Phases 22 onward and
use these abstractions unchanged.

## See also

- [Case Study 01: Lift-And-Shift Architecture](../../Case%20Studies/01-Lift-And-Shift-Architecture.md)
- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Case Study 05: Multi-Tenancy As Optional](../../Case%20Studies/05-Multi-Tenancy-As-Optional.md)
- [Prepaid wallets — event-sourced core](../../docs/articles/prepaid-wallets-event-sourcing.md)
- [Understanding prepaid wallets (narrative)](../../docs/narratives/understanding-prepaid-wallets.md)
- `PolarSharp.PrepaidWallets` — the aggregate + handlers built on these abstractions

## License

MIT. (c) Molls and Hersh, LLC. 2026.
