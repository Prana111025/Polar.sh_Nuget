# PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore

Provider-agnostic EF Core event store + snapshot store for prepaid wallets. Ships the abstract
`WalletEventStoreDbContext` + the `EfWalletEventStore` / `EfWalletSnapshotStore` implementations
hosts plug into their concrete DbContext.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore
```

Plus a provider package (`.SqlServer`, `.Sqlite`, `.PostgreSQL`, `.MariaDb`, `.CosmosDb`) — the
provider package ships in Phase 21 and provides a `UseXxxWalletEventStore(...)` helper that
registers the concrete `DbContext` against the appropriate database.

## Quickstart

```csharp
// Phase 20 — wire your own concrete DbContext + Sqlite for unit-test scenarios:
public sealed class MyWalletEventStoreDbContext(DbContextOptions<MyWalletEventStoreDbContext> opts)
    : WalletEventStoreDbContext(opts) { }

builder.Services.AddDbContext<MyWalletEventStoreDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("Wallets")!));

builder.Services
    .AddPolarPrepaidWallets()
    .AddEfCoreWalletEventStore<MyWalletEventStoreDbContext>();
```

In Phase 21, the provider packages collapse the boilerplate to a single line per provider:

```csharp
builder.Services
    .AddPolarPrepaidWallets()
    .UseSqlServerWalletEventStore(builder.Configuration.GetConnectionString("Wallets")!);
```

## What this package contains

- `WalletEventStoreDbContext` — abstract base with the schema declaration
  (`wallet_events` + `wallet_snapshots`), unique indexes on `(wallet_id, sequence_no)` and
  `(wallet_id, idempotency_key)`, and composite key `(wallet_id, version)` on snapshots.
- `WalletEventRecord` + `WalletSnapshotRecord` — EF entity types.
- `EfWalletEventStore` — `IWalletEventStore` impl. Optimistic concurrency via the unique
  sequence index; idempotency via the unique key index. `DbUpdateException` is translated to
  `WalletConcurrencyConflictException` so the MediatR retry behavior recovers.
- `EfWalletSnapshotStore` — `IWalletSnapshotStore` impl.
- `AddEfCoreWalletEventStore<TContext>()` — DI extension binding the abstract context to the
  host's concrete DbContext.

The schema:

```
wallet_events
    id, wallet_id, sequence_no, event_type, event_payload_json,
    idempotency_key, occurred_at, actor_user_id
  UNIQUE (wallet_id, sequence_no)
  UNIQUE (wallet_id, idempotency_key)

wallet_snapshots
    wallet_id, version, customer_id, tenant_id, currency,
    balance_tokens, status_code, opened_at, last_activity_at, taken_at
  PRIMARY KEY (wallet_id, version)
```

**Lift-safe (zero PolarSharp.* deps)** — depends only on `PolarSharp.PrepaidWallets` + EF Core
+ EF Core Relational. Hosts can lift the wallet ledger to a non-PolarSharp host by keeping the
same schema.

## Status

v1.3.0 — Phase 20 ships the provider-agnostic base. The 5 EF Core provider packages
(`.SqlServer` / `.Sqlite` / `.PostgreSQL` / `.MariaDb` / `.CosmosDb`) ship in Phase 21 with
matching integration tests via Testcontainers.

## See also

- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Prepaid wallets — event-sourced core](../../docs/articles/prepaid-wallets-event-sourcing.md)
- `PolarSharp.PrepaidWallets` — the wallet aggregate
- `PolarSharp.PrepaidWallets.EventStore.Marten` — Marten alternative on Postgres

## License

MIT. (c) Molls and Hersh, LLC. 2026.
