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

## D-007 — Three-package UI split for component reuse across Web + MAUI

**Locked:** 2026-05-13 (see `PLAN.md` § "v1.4.0 — Test App Refresh + EcommerceStorefronts WebComponents" — UI architecture subsection).

Reusable PolarSharp UI components ship as a **three-package set**, not one:

- **`PolarSharp.UI.Components.Core`** — plain class library; pure C# view models, state records, validator helpers, no rendering. Both Web + MAUI consume it. Targets `net10.0`.
- **`PolarSharp.UI.Components.Web`** — Razor Class Library; Blazor components for Server / WASM / Hybrid. The web flagship demo consumes it. Independently testable via bUnit. Targets `net10.0`.
- **`PolarSharp.UI.Components.Maui`** — MAUI control library; mobile-first layouts, native pages, gesture handlers, platform features (camera, biometric, push). Targets `net10.0-{android,ios,maccatalyst,windows*}`. Reserved for v1.5.0 alongside the `PolarMauiDemo` flagship.

**Rationale:** template packs (`<PackageType>Template</PackageType>` for `dotnet new`) and RCLs ship via different consumption mechanisms; mixing them breaks both. Web vs MAUI have fundamentally different edge concerns (transport, auth, navigation, native capabilities); forcing them into one package creates noise. The Core package captures everything that's hosting-model-agnostic so cross-platform reusability is built in from day one without compromising either platform's idioms.

This split is distinct from `PolarSharp.EcommerceStorefronts.WebComponents` — that package ships customer-facing Stencil Web Components for embed-anywhere surfaces (tenant marketplaces + partner sites), while this trio ships admin-facing Blazor + MAUI components for internal flagship + tenant-admin / SaaS-admin / merchant-admin Razor pages.
