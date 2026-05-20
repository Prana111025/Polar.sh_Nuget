# PolarSharp.PrepaidWallets.EventStore.Marten

Marten-native event store + snapshot store for prepaid wallets, on Postgres.

## Install

```sh
dotnet add package PolarSharp.PrepaidWallets.EventStore.Marten
```

## Quickstart

```csharp
builder.Services
    .AddPolarPrepaidWallets()
    .UseMartenWalletEventStore(builder.Configuration.GetConnectionString("Wallets")!);
// Optional second arg: schemaName (defaults to "polar_marten_wallet")
```

## What this package does

Backs the wallet aggregate's event stream with Marten — Postgres-native event sourcing. Per
Case Study 02 §1 ("Choose the event-store backend"), Marten is the natural fit when the host
already runs PostgreSQL: native event-streaming primitives align perfectly with the wallet's
aggregate model, no impedance mismatch.

**Implementations shipped:**
- `MartenWalletEventStore` — implements `IWalletEventStore` via Marten event streams.
  Optimistic concurrency rides on Marten's expected-version arg; idempotency is enforced by
  reading the stream before appending.
- `MartenWalletSnapshotStore` — implements `IWalletSnapshotStore`. Snapshots stored as Marten
  documents (`MartenWalletSnapshotDocument`), keyed by wallet id; later writes overwrite earlier
  snapshots only when they advance the version.
- `UseMartenWalletEventStore(...)` — DI extension that configures Marten with the eight wallet
  event types registered and the snapshot document mapped.

**Lift-safe (zero PolarSharp.* deps)** — the package depends on `PolarSharp.PrepaidWallets` +
Marten directly; no `PolarSharp.MultiTenant`, no `PolarSharp.Reporting`. Hosts running on
Postgres with PolarSharp's RLS providers get defense-in-depth automatically.

## Phase 20 unit test coverage

Phase 20 unit tests cover the pure types (snapshot document round-trip, DI extension wiring).
End-to-end Marten + Postgres integration tests via Testcontainers ship in Phase 21.

## Status

v1.3.0 — Phase 20 ships the Marten implementation; Phase 21 adds the integration-test suite.

## See also

- [Case Study 02: Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Prepaid wallets — event-sourced core](../../docs/articles/prepaid-wallets-event-sourcing.md)
- `PolarSharp.PrepaidWallets` — the wallet aggregate this stores
- `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.PostgreSQL` — EF Core alternative on Postgres (Phase 21)

## License

MIT. (c) Molls and Hersh, LLC. 2026.
