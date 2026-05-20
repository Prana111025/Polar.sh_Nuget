# DECISIONS.md

Durable architecture and process decisions. Agents must not relitigate decisions recorded here unless the human user explicitly reopens them.

Each entry: the decision, date locked, where the long-form rationale lives, and any cross-references.

---

## D-001 — Polar.sh operates as Merchant of Record (MoR); PolarSharp does NOT compute tax for Polar-checkout flows

**Locked:** 2026-05-20 (see `PLAN.md` "Architectural framing — Polar.sh as Merchant of Record (MoR)" section).

For every transaction that flows through Polar's checkout, Polar calculates the correct sales tax / VAT / GST, collects it from the customer, files the returns, and remits to the tax authority on the merchant's behalf. The tenant (merchant) does NOTHING for tax compliance on Polar-checkout transactions. PolarSharp therefore does NOT ship a tax-computation provider as a default for the standard checkout path — shipping one would imply merchants need to think about tax, which contradicts Polar's value prop.

**Exception:** three scenarios bypass Polar's MoR coverage and need PolarSharp tooling — wallet-only customer checkout, hybrid checkout, and Settlement Mode D (TenantPrefundedWallet). These are addressed by the WTR framework (D-002).

**Cross-refs:**
- Inline XML doc on `IPolarBusinessProfileService.BuildBankingSetupDeepLink` (same principle for banking: PolarSharp does NOT talk to Stripe).
- `docs/articles/ecommerce-catalog.md` § "Banking and payouts".
- `CLAUDE.md` § "Critical framing rules — DO NOT VIOLATE in any Narrative or doc".

---

## D-002 — Wallet Tax Responsibility (WTR) framework addresses the three MoR coverage gaps via two-actor reporting

**Locked:** 2026-05-20 (see `PLAN.md` § "Wallet Tax Responsibility (WTR) framework").

A single framework with two actor perspectives (tenant-perspective wallet-only sales tax + SaaS-perspective per-tenant revenue tax) using identical primitives because the architectures are isomorphic. Components: hard-gate acknowledgments, funding-source-aware estimator services with quarterly-updated rate tables (labeled "estimate only"), comprehensive quarterly + annual + accumulative tax-owed reports, deadline-reminder hosted services, settlement-mode-aware revenue ledger.

**Ship schedule:** Phase 22.5, after PrepaidWallets bridges (Phase 22).

**TaxJar binding decision:** keep the `IStorefrontTaxProvider` abstraction; keep the TaxJar scaffold (no longer suppressed by vuln concerns since `Directory.Packages.props` pins RestSharp 114.0.0 transitively); re-evaluate whether to fill in the binding in v1.4.0 based on real-world demand.

---

## D-003 — Phase 20 wallet event signatures stamp funding-source provenance immutably

**Locked:** 2026-05-20 (see archived `docs/archive/COORDINATION-NOTE-FROM-MAIN-SESSION.md`; implemented in commit `8170c8d`).

Every `WalletFunded` + `WalletCredited` event stamps a `FundingSourceKind` enum value at the time of issue. Every `WalletDebited` event stamps `IReadOnlyList<FundingSourceAllocation> FundingSources` computed via FIFO allocation across the wallet's funding buckets. This information CANNOT be recomputed from event history at the scale the WTR framework requires, so it must be present on the event. Wallet aggregates track remaining-balance-per-bucket as part of aggregate state, reconstructible from event replay; on snapshot, the bucket list snapshots with everything else.

**FundingSourceKind enum** (in `PolarSharp.PrepaidWallets.Abstractions`): `CustomerCashFunded`, `GiftCardActivation`, `RefundAsCredit`, `TenantPromotionalGrant`, `TenantBugFixCompensation`, `TrialCredit`.

**Index requirement:** EF Core event-store provider migrations include `ix_wallet_events_tenant_id_occurred_at (tenant_id, occurred_at DESC)`. Marten event-store applies the equivalent computed index.

Events are immutable once shipped; retrofitting these fields would require a per-tenant schema migration on every wallet ledger, which is why the choice was locked at Phase 20 even though the WTR consumer ships in Phase 22.5.

---

## D-004 — Lift-and-shift namespace separator + CI dependency guard for monorepo features designed for eventual extraction

**Locked:** 2026-05-18 (see `Case Studies/01-Lift-And-Shift-Architecture.md`).

Packages that may later move to standalone repositories (currently: most PrepaidWallets packages, the WTR framework, EcommerceStorefronts core) use the `PolarSharp.Xxx.Polar.Yyy` namespace pattern. The `.Polar.` infix marks the lift-shift boundary: code in `PolarSharp.Xxx` (the lift-safe core) MUST NOT reference code in `PolarSharp.Xxx.Polar.*` (the bridge layer that couples to Polar.sh-specific concerns); code below the infix is free to depend upward but never sideways.

A CI dependency-guard check enforces this at build time so accidental sideways coupling can never make it to main.

**Cross-refs:** `Case Studies/01-Lift-And-Shift-Architecture.md`; `Case Studies/02-Event-Sourced-Wallet-With-Economic-Modeling.md` § "Polar.sh as the SaaS-tenant on-ramp".

---

## D-005 — Every tenant-owned entity satisfies the 5-layer tenant isolation acceptance criteria

**Locked:** 2026-05-19 (see `PLAN.md` § "Architectural foundations" within Phase 3 of v1.4.0; see also memory `feedback_tenant_isolation_every_new_entity.md`).

Every new entity / column introduced by any backend addition must explicitly satisfy:

1. **EF query filter** — `ITenantOwned` interface + global filter in `TenantAwareDbContextBase`
2. **Database-level RLS** — RLS policy in initial EF migration for SqlServer + Postgres providers (`CREATE SECURITY POLICY` / `ENABLE ROW LEVEL SECURITY` + `CREATE POLICY`)
3. **SQLite per-tenant `.db` file placement** via existing `SqlitePerTenantDbContextFactory`
4. **MariaDB app-layer filter** (no native RLS) — query-filter-only enforcement; documented in the package README
5. **Cosmos `/tenantId` partition key**

Plus: single-tenant mode behavior (filter no-op; entity keeps `TenantId` column). Plus: cross-tenant isolation regression test per the existing `CrossTenantIsolationTests` template.

These acceptance criteria are not optional; they apply to every new entity, no matter how trivial.

**Open gap:** TASK-V20-012 — RLS DDL is not yet present in any initial migration. Layer 1 (EF query filter) is the only line of defense today. Closing this gap is a v2.0 priority.

---

## D-006 — Agent coordination protocol for parallel branches

**Locked:** 2026-05-19 (see `AGENT-COORDINATION.md` for the full file-ownership matrix + branch-naming + shared-file lockout + memory-write rule + pre-merge gate).

Parallel agents working on different feature areas (e.g. wallet vs. storefronts vs. docs) MUST:

- Use the `agent/<area>/<phase>-<short-slug>` branch-naming convention
- Stay inside their declared file-ownership matrix; shared files (`PolarSharp.slnx`, `Directory.Packages.props`, `CHANGELOG.md`, planning docs) require lockout coordination via the main coordinator session
- Write `agent-memory/` notes that the main session reads when consolidating; agents do NOT write to each other's memory directories
- Run the pre-merge gate (solution-wide `dotnet build` + `dotnet test`) before opening a PR; the solution sweep is authoritative — per-csproj invocation outside the slnx is not (see commit `9472798`)

Solo sessions can skim this and otherwise skip; the protocol exists for multi-agent days.

---

## D-008 — Event-sourced aggregates + read-model projections; gift cards / loyalty / referrals are their own aggregates, not wallet funding-source stamps

**Locked:** 2026-05-20 (see TASK-V14-010 / V14-011 / V14-012 in TASKS.md; surfaced by user question during the testing overhaul session).

PolarSharp uses three layers of projection in every event-sourced aggregate family:

1. **Aggregate-internal projection** — the aggregate's own `Apply(IEvent)` rebuilds in-memory state during load. Shipped today for the wallet aggregate (`WalletAggregate` projects funding-bucket state across the event history).
2. **Snapshots** — periodic point-in-time materialisations of aggregate state stored separately from the event stream so loads don't have to replay every event from #0. Shipped today for the wallet aggregate (`WalletSnapshot` + `IWalletSnapshotStore`) on both EF Core and Marten backends.
3. **Read-model projections** — denormalised views that queries / UIs / reports consume directly. Driven by an event-stream subscription (Marten projection daemon; EF Core polling-based replay). Each projection lives in a dedicated table optimised for its query shape. NOT shipped today; TASK-V14-010 closes this gap for the wallet aggregate.

### Locked principle: balance-tracking primitives are their own aggregates

Things that look like "values stored in a wallet" but have their own lifecycles MUST be modelled as separate event-sourced aggregates, NOT as wallet funding-source stamps. Specifically:

- **Gift cards** get a `GiftCardAggregate` with events `GiftCardActivated` / `Redeemed` / `Transferred` / `Expired` / `Escheated` / `Replaced`. The wallet event stream references the gift card by id (`Option<Guid> SourceGiftCardId`) when tokens flow in. Tracked as TASK-V14-011.
- **Loyalty accounts** get a `LoyaltyAccountAggregate` with events for point accrual, tier progression (Bronze → Silver → Gold), tier downgrade, point expiry, redemption. Tracked as TASK-V14-012.
- **Referrals** get a `ReferralAggregate` with events for attribution (referee signs up via referrer's link), windowed-payout (referrer earns X% of referee's purchases for first N days), multi-step chain attribution. Tracked as TASK-V14-012.

Each gets its own event stream + read-model projections per the three-layer rule above. Each ships with a Polar bridge (`PolarSharp.GiftCards.Polar.Checkout`, etc.) that wires the redemption / payout paths into Polar's checkout flow.

### Why this matters

The wallet `FundingSourceKind` stamps (`GiftCardActivation`, `TenantPromotionalGrant`, `TenantBugFixCompensation`, `TrialCredit`) document where a token came from at the moment it entered a wallet — they're correct for that. But they CANNOT serve as the source of truth for:

- Outstanding-gift-card-liability accounting (tenant must report unredeemed gift-card balance every period)
- Unclaimed-property / escheatment compliance (US state laws differ on when an unredeemed gift card balance must transfer to the state's unclaimed-property fund)
- Loyalty tier progression history (court / regulatory disputes may require "show me when this customer transitioned from Silver to Gold and based on what events")
- Referral attribution audits (when did the attribution window start? when did it close? was the chain truncated at N levels?)

These are queries against the GIFT CARD's history, not the wallet's history. They need their own aggregates with their own projections.

### What about MoR / WTR tax treatment

D-001 (Polar.sh as MoR) and D-002 (WTR framework) still apply. The funding-source stamps on wallet events stay — they're how the WTR estimator distinguishes "fully taxable on the new sale" from "discount-reduces-basis" treatment. The gift card / loyalty / referral aggregates ADD lifecycle tracking on top of the wallet's funding-source-stamp model; they don't replace it.

### Anti-pattern to reject in review

A PR that adds `FundingSourceKind.NewlyInventedSource` for a thing that has its own lifecycle (e.g. `EmployeePerksAccount`, `RetailerCobrandedCard`, `SubscriptionFreeMonth`) is a code smell. Push back: should this be its own aggregate?

The funding-source enum is for "this is the bucket of money that paid for this debit" — single point-in-time provenance stamping. The aggregate family is for "this is a thing with a life beyond a single moment."

### Marten implementations MUST leverage Marten-native event-sourcing features

**Locked 2026-05-20 (user direction).** Marten is purpose-built for event sourcing against Postgres. Implementations of event-sourced aggregates in `PolarSharp.*.EventStore.Marten` packages MUST use Marten's native primitives rather than treating Marten as "Postgres-with-streams":

| Marten primitive | Required usage |
|---|---|
| **`IDocumentStore.Events.AppendAsync`** + `ExpectedVersion` | Native optimistic concurrency; do NOT roll our own version-check by reading the stream first |
| **`AggregateStreamAsync<TAggregate>`** | Aggregate-state rebuild during load; replaces our manual `LoadAsync` + `Apply` loop where Marten can do the equivalent natively |
| **Projection daemon** (`Marten.Events.Daemon`) | Background async projection runner for read-model projections (the TASK-V14-010 family). Marten's daemon handles checkpointing, rebuild-from-zero, error recovery — do NOT reinvent any of that |
| **`IProjection<T>` + `MultiStreamProjection<T>`** | Read-model projections (single-stream and cross-stream). Each PolarSharp aggregate family ships its projections in this shape |
| **`InlineProjection`** | Transactionally-consistent projections updated in the same Postgres transaction as the event append. Use for projections that MUST reflect the latest event before the append returns (rare; usually async daemon is preferred) |
| **Subscription-based event handlers** (`IEventHandler<T>`) | Saga / process-manager pattern. Use when one aggregate's event triggers commands on another aggregate |
| **Event metadata projections** | Marten can index `tenant_id` / `actor_user_id` / `correlation_id` from event metadata for fast cross-stream queries. Use this instead of manually indexing on a denormalised column |
| **Tenancy-aware document stores** (`StoreOptions.Schema.For<>().MultiTenanted()`) | Per-tenant schema isolation. Pairs with the 5-layer tenant isolation acceptance criterion (D-005) at the Marten layer |
| **Stream archiving** (`StoreOptions.Events.Archive`) | Move closed-aggregate streams (e.g. expired gift cards, escheated cards) to cold storage; queries still see the unified history. Important for unclaimed-property compliance audit trails |
| **`AsyncDaemon` projection rebuild API** | Production rebuild path when a projection's shape changes or its store gets corrupted. Build the rebuild path into every projection from day one |

### What EF Core providers do differently

EF Core providers (`PolarSharp.*.EventStore.EntityFrameworkCore.*`) do NOT have Marten's native event-sourcing primitives. They get:

- A `wallet_events` table with append-only writes (manual implementation)
- Snapshot table with periodic point-in-time materialisation (manual implementation)
- A polling-based projection runner for read-model projections (manual implementation; less feature-rich than Marten's daemon)
- A `tenant_id` index for cross-stream queries (manual migration)
- Per-provider quirks: MariaDb's history-repo workaround, Cosmos partition-key strategy, etc.

The EF Core providers are a fallback for hosts who can't run Postgres. **Marten is the recommended backend for new event-sourced PolarSharp deployments** precisely because it gives us all the above for free.

### Anti-pattern to reject in Marten-impl review

A PR that adds a `MartenXxxEventStore` class with manual stream-reading + manual concurrency check + a separate polling-based projection runner is leaving Marten's value on the table. Reject and ask: which of the above Marten primitives should this be using instead?

The current `MartenWalletEventStore` (shipped Phase 20) implements basic append + load + snapshot but does NOT yet use the projection daemon, inline projections, multi-stream projections, or tenancy-aware stores. Refactoring it to fully leverage Marten is part of TASK-V14-010.

---

## D-009 — Search always works out of the box; cloud + heavyweight options are upgrades, not prerequisites

**Locked:** 2026-05-20 (user direction during the testing overhaul: storefronts must work the moment `AddPolarStorefrontsCore()` is called, with no required cloud setup or self-host docker container).

PolarSharp ships **five** `IStorefrontSearchProvider` options across a tiered fallback chain. The first tier is the always-on default; the rest are opt-in upgrades.

| Tier | Provider | Where it lives | When to pick it |
|---|---|---|---|
| 1 | `InMemoryStorefrontSearchProvider` | `PolarSharp.EcommerceStorefronts` (storefront-core; not a separate package) | Always-on default. Catalogs up to ~10K products. Zero external infrastructure. |
| 2a | `PolarSharp.EcommerceStorefronts.Search.Sqlite` | New scaffold (Phase 14.x) | Pairs with default per-tenant SQLite catalog. SQLite FTS5. No new service. |
| 2b | `PolarSharp.EcommerceStorefronts.Search.PostgreSql` | New scaffold (Phase 14.x) | Pairs with Postgres catalog. tsvector/tsquery + GIN index. No new service. |
| 3 | `PolarSharp.EcommerceStorefronts.Search.Elasticsearch` | New scaffold (Phase 14.x) | "Big-name OSS, Docker-hostable" — Elasticsearch or OpenSearch. Mature, rich faceting, vector search. |
| 4 | `PolarSharp.EcommerceStorefronts.Search.AzureAiSearch` | New scaffold (Phase 14.x) | "Cloud big-name" — Microsoft's managed search. Semantic ranker + vector + Azure OpenAI integration. |

**Deprecated:** `PolarSharp.EcommerceStorefronts.Search.MeiliSearch` (kept for back-compat; will not receive an implementation; new deployments pick from the 5 alternatives above). Per user direction 2026-05-20: prefer hyperscaler-backed or well-known-OSS options over Meilisearch's continued-existence risk.

### Core architectural rule

The DI registration in `AddPolarStorefrontsCore()` uses `TryAddScoped<IStorefrontSearchProvider, InMemoryStorefrontSearchProvider>()`. Bridge packages register their own provider BEFORE the core call, so `TryAdd` respects the override:

```csharp
services.UseAzureAiSearchStorefrontSearch(opts => { … });   // bridge registers IStorefrontSearchProvider
services.AddPolarStorefrontsCore();                          // sees the prior registration; doesn't override
```

### What this rule prevents

A consumer who calls `AddPolarStorefrontsCore()` and forgets to register a search bridge gets a working storefront with in-memory search — NOT a runtime `InvalidOperationException: Unable to resolve service for type IStorefrontSearchProvider` (the Trap-1 pattern in `LARGE-PROJECT-BEST-PRACTICES.md`). Search degrades gracefully across the tier ladder; it never silently disappears.

### Anti-pattern to reject in review

A storefront PR that requires a tenant to provision an external search service before the storefront can render product results violates this decision. Search must work with zero external setup; richer search is opt-in.

---

## D-007 — Three-package UI split for component reuse across Web + MAUI

**Locked:** 2026-05-13 (see `PLAN.md` § "v1.4.0 — Test App Refresh + EcommerceStorefronts WebComponents" — UI architecture subsection).

Reusable PolarSharp UI components ship as a **three-package set**, not one:

- **`PolarSharp.UI.Components.Core`** — plain class library; pure C# view models, state records, validator helpers, no rendering. Both Web + MAUI consume it. Targets `net10.0`.
- **`PolarSharp.UI.Components.Web`** — Razor Class Library; Blazor components for Server / WASM / Hybrid. The web flagship demo consumes it. Independently testable via bUnit. Targets `net10.0`.
- **`PolarSharp.UI.Components.Maui`** — MAUI control library; mobile-first layouts, native pages, gesture handlers, platform features (camera, biometric, push). Targets `net10.0-{android,ios,maccatalyst,windows*}`. Reserved for v1.5.0 alongside the `PolarMauiDemo` flagship.

**Rationale:** template packs (`<PackageType>Template</PackageType>` for `dotnet new`) and RCLs ship via different consumption mechanisms; mixing them breaks both. Web vs MAUI have fundamentally different edge concerns (transport, auth, navigation, native capabilities); forcing them into one package creates noise. The Core package captures everything that's hosting-model-agnostic so cross-platform reusability is built in from day one without compromising either platform's idioms.

This split is distinct from `PolarSharp.EcommerceStorefronts.WebComponents` — that package ships customer-facing Stencil Web Components for embed-anywhere surfaces (tenant marketplaces + partner sites), while this trio ships admin-facing Blazor + MAUI components for internal flagship + tenant-admin / SaaS-admin / merchant-admin Razor pages.
