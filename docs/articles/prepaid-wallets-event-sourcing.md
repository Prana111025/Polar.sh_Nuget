# Prepaid wallets — event-sourced core

The PolarSharp.PrepaidWallets feature is built on Case Study 02 ("Event-Sourced Wallet With
Comprehensive Economic Modeling"). This article is the developer-facing reference for the
Phase 20 core: the four lift-safe packages that contain the aggregate, the commands and queries,
and the two storage providers (Marten on Postgres; EF Core for everything else).

For a non-technical walkthrough of the same material, see the
[Understanding prepaid wallets](narratives/understanding-prepaid-wallets.md) Implementation
Narrative.

## Packages

| Package | Purpose | Lift-safe? |
|---|---|---|
| `PolarSharp.PrepaidWallets.Abstractions` | Contracts: events, commands, queries, interfaces, value objects, exceptions. Zero PolarSharp.* deps. | Yes |
| `PolarSharp.PrepaidWallets` | The wallet aggregate, MediatR command/query handlers, behaviors, projections, snapshot policy, in-memory default stores. | Yes |
| `PolarSharp.PrepaidWallets.EventStore.Marten` | Marten-backed event store + snapshot store for Postgres hosts. | Yes |
| `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore` | Provider-agnostic EF Core base. Provider packages (`.SqlServer`, `.Sqlite`, `.PostgreSQL`, `.MariaDb`, `.CosmosDb`) ship in Phase 21. | Yes |

Per Case Study 01 ("Lift-And-Shift Architecture"), every package above has **zero**
`PolarSharp.*` dependencies outside the wallet family. A consumer can pull the four packages,
register them, and have a complete wallet system without needing any other PolarSharp package.

## Architecture at a glance

```
┌─────────────────────────────────────────────────────────────┐
│ Wallet aggregate (Wallet.cs)                                 │
│ - balance, status, version (state)                           │
│ - TryFund / TryDebit / TryCredit / TryRefund / TryFreeze ... │
│ - returns Result<IWalletEvent, CommandError>; never throws   │
│   on expected domain errors                                  │
└──────────────────────────┬───────────────────────────────────┘
                           │ events
                           ▼
┌─────────────────────────────────────────────────────────────┐
│ MediatR pipeline                                             │
│ ConcurrencyRetryBehavior  → ValidationBehavior →             │
│ LoggingBehavior → CommandHandler                             │
└──────────────────────────┬───────────────────────────────────┘
                           │ events
                           ▼
┌─────────────────────────────────────────────────────────────┐
│ IWalletEventStore  +  IWalletSnapshotStore                   │
│  - in-memory (default; tests + single-process scenarios)     │
│  - Marten (Postgres)                                         │
│  - EF Core (SQL Server / SQLite / Postgres / MariaDB / Cosmos)│
└─────────────────────────────────────────────────────────────┘
```

The wallet **aggregate** is the only place invariants live (balance ≥ 0, no debit when frozen,
no commands when closed, idempotency replay returns the original outcome). Persistence and
projection are downstream concerns; the aggregate folds its event stream into state and emits new
events.

## Events

| Event | When | Carries |
|---|---|---|
| `WalletOpened` | First event of every stream. | Customer id, tenant scope, currency. |
| `WalletFunded` | Customer payment cleared. | Token amount + bonus, funding source, **full economic breakdown** (customer charged / processor fee / SaaS profit / tenant absorbed / tenant net, all in cents), plus the funding-terms JSON snapshot for dormancy / refund eligibility. |
| `WalletDebited` | Order line / invoice / subscription tick paid from the wallet. | Token amount, free-form `TargetKind` + `TargetId`, resulting balance. |
| `WalletCredited` | Non-funding credit (admin manual, PO grant, promo). | Token amount, free-form reason, optional related PO id. |
| `WalletRefunded` | Refund of a prior funding event. | Tokens refunded, original-funding sequence number, refunded-to-customer cents, surcharge cents, SaaS share cents. |
| `WalletFrozen` / `WalletUnfrozen` | Operator suspends / resumes the wallet. | Free-form reason. |
| `WalletClosed` | Terminal close. | Free-form reason. |

Every event implements `IWalletEvent` and carries: `WalletId`, `SequenceNo`, `OccurredAt`,
`ActorUserId`, `IdempotencyKey`, `EventType` discriminator. All events are immutable `sealed
record` types, AOT-safe via the source-generated `WalletJsonContext`.

## Commands and queries

Commands are MediatR `IWalletCommand<WalletCommandResult>` records. The pipeline applies
concurrency retry → validation → logging → handler. Handlers return
`Result<CommandSuccess, CommandError>` instead of throwing on expected outcomes (insufficient
funds, frozen wallet, validation failure). Exceptions surface only for programmer errors and
infrastructure failures (concurrency conflict, idempotency-mismatch, unknown event type).

Queries: `GetWalletStateQuery`, `GetWalletBalanceQuery`, `GetWalletHistoryQuery`. Each returns
an `Option<T>` or a paginated list — never throws on missing wallets.

## Idempotency and concurrency

Every command carries an `IdempotencyKey`. The aggregate tracks the keys it has seen; the
event store enforces a unique `(wallet_id, idempotency_key)` index so replays after a
network retry collapse to the original event. A replay carrying a *different* payload throws
`IdempotencyKeyMismatchException` — the caller's key-generation is broken.

Optimistic concurrency rides on the unique `(wallet_id, sequence_no)` index. A second writer
with a stale read fails with `WalletConcurrencyConflictException`; the MediatR
`ConcurrencyRetryBehavior` re-loads the aggregate and re-applies the command up to three times
before giving up.

## Snapshots

`StrideSnapshotPolicy` writes a snapshot every N events (default 50; tune to 10 for Cosmos
hosts where full replay is RU-expensive). Aggregate loading prefers
`IWalletSnapshotStore.LoadLatestAsync` plus a tail of post-snapshot events; falls back to a
full event-stream replay when no snapshot exists. The `SnapshotEquivalenceTests` suite proves
the two paths produce identical state.

## Storage backends

### Marten (Postgres)

```csharp
builder.Services
    .AddPolarPrepaidWallets()
    .UseMartenWalletEventStore(builder.Configuration.GetConnectionString("Wallets")!);
```

Marten registers all eight wallet event types and a `MartenWalletSnapshotDocument` mapped to the
configured schema (default `polar_marten_wallet`). Optimistic concurrency rides on Marten's own
expected-version checks; idempotency is enforced by reading the stream before appending.

### EF Core

```csharp
builder.Services.AddDbContext<MyWalletEventStoreDbContext>(opts => opts.UseSqlServer(...));
builder.Services
    .AddPolarPrepaidWallets()
    .AddEfCoreWalletEventStore<MyWalletEventStoreDbContext>();
```

`MyWalletEventStoreDbContext` derives from the shipped `WalletEventStoreDbContext` and adds any
provider-specific configuration. Provider packages (`.SqlServer`, `.Sqlite`, `.PostgreSQL`,
`.MariaDb`, `.CosmosDb`) ship the concrete `DbContext` plus the matching `UseXxxWalletEventStore`
helper in Phase 21.

The schema (declared in `WalletEventStoreDbContext.OnModelCreating`):

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

## What's NOT in Phase 20

The Phase 20 core delivers the event-sourced spine. The following ship in later phases per
PLAN.md:

- **Phase 21**: EF Core provider packages (SqlServer / Sqlite / PostgreSQL / MariaDb / CosmosDb)
  + Marten + EF Core integration-test suites via Testcontainers.
- **Phase 22**: Polar bridges (`PolarSharp.PrepaidWallets.Polar.*`) — Polar.sh funding processor,
  Polar identity bridge, Polar.Reporting integration, Polar.GraphQL surface, lift-shift CI guard.
- **Phase 23**: Notifications dispatcher + 6 channel providers (SendGrid, MailKit, Azure Email,
  AWS SES, Twilio SMS, Webhook); 2 template engines (Scriban + Fluid).
- **Phase 24**: Funding processors (Stripe + PayPal); maintenance-fee hosted service;
  refund-conversion policy; balance-escalation framework + tenant + customer policies.
- **Phase 25+**: Reporting / GraphQL / Blazor RCLs / SaaSInvoicing / PrefundedTenant (Option D).

## Related case studies

- [Case Study 01 — Lift-And-Shift Architecture](../../Case%20Studies/01-Lift-And-Shift-Architecture.md)
- [Case Study 02 — Event-Sourced Wallet With Economic Modeling](../../Case%20Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md)
- [Case Study 05 — Multi-Tenancy as Optional](../../Case%20Studies/05-Multi-Tenancy-As-Optional.md)
- [PrepaidWalletsLiftAndShift.md](../../PrepaidWalletsLiftAndShift.md) — the operational lift procedure
