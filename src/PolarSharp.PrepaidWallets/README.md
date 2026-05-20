# PolarSharp.PrepaidWallets

Event-sourced `Wallet` aggregate + MediatR command/query handlers + behaviors + snapshot strategy
+ in-memory default event/snapshot stores.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets
```

## Quickstart

```csharp
builder.Services.AddPolarPrepaidWallets();
// Default: in-memory event store + in-memory snapshot store. For production, replace via:
//   .UseMartenWalletEventStore(connectionString)                       // Postgres
//   .AddEfCoreWalletEventStore<MyConcreteWalletEventStoreDbContext>()  // SQL Server / SQLite / etc.

// Then send a command via MediatR:
var result = await mediator.Send(new OpenWalletCommand(
    WalletId.NewId(),
    customerId,
    Option<Guid>.None,
    Currency: "USD",
    ActorUserId: currentUser.Id,
    IdempotencyKey: IdempotencyKey.Create("open-wallet-for-cust-42")));
```

## What this package contains

- The `Wallet` aggregate (`PolarSharp.PrepaidWallets.Domain.Wallet`) with explicit invariants —
  balance ≥ 0, no debit when frozen, no commands when closed, idempotency replay returns the
  recorded outcome.
- MediatR command handlers for all 8 wallet commands.
- MediatR query handlers for all 3 wallet queries.
- Pipeline behaviors: `ConcurrencyRetryBehavior`, `ValidationBehavior`, `LoggingBehavior`.
- `WalletAggregateLoader` — prefers snapshot + delta-replay over full event-stream replay.
- `StrideSnapshotPolicy` — default 50 events between snapshots; tune via constructor.
- `InMemoryWalletEventStore` + `InMemoryWalletSnapshotStore` for tests and single-process use.
- `JsonWalletEventSerializer` — System.Text.Json source-generated, AOT-safe.
- FluentValidation validators for every command.

**Lift-safe (zero PolarSharp.* deps)** — the core depends only on
`PolarSharp.PrepaidWallets.Abstractions`. Per Case Study 01, the wallet feature can be lifted out
of PolarSharp with a short script. Hosts on PolarSharp.MultiTenant.Identity get the Polar bridges
as separate packages (`PolarSharp.PrepaidWallets.Polar.*`).

## Storage backends

Phase 20 ships the in-memory default. Hosts pick one production backend:

- `PolarSharp.PrepaidWallets.EventStore.Marten` — Postgres, native event-sourcing primitives.
- `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore` + a provider package (`.SqlServer`,
  `.Sqlite`, `.PostgreSQL`, `.MariaDb`, `.CosmosDb`) — Phase 21.

## Status

v1.3.0 — Phase 20 ships the aggregate, handlers, behaviors, snapshot policy, in-memory stores,
JSON serializer, and FluentValidation validators. Notifications dispatcher (Phase 23), funding
processors (Phase 24), and the higher-level integrations land in subsequent phases per PLAN.md.

## See also

- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Prepaid wallets — event-sourced core](../../docs/articles/prepaid-wallets-event-sourcing.md)
- [Understanding prepaid wallets (narrative)](../../docs/narratives/understanding-prepaid-wallets.md)
- `PolarSharp.PrepaidWallets.Abstractions` — the contracts
- `PolarSharp.PrepaidWallets.EventStore.*` — storage providers (Marten + 5 EF Core variants)

## License

MIT. (c) Molls and Hersh, LLC. 2026.
