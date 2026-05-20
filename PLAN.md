# PLAN.md

Authoritative current technical plan for this repository.

Use the Agile Planning Template from AGENTS.md. Keep only the active plan here. Move historical decisions to DECISIONS.md and completed status to PROGRESS.md.

---

## Active plan — PolarSharp v1.4.0 (in progress)

**Status:** in progress (2026-05-20).
**Repository:** Polar.sh_Nuget (path: `/Users/mollsandhersh/Repos/Polar.sh_Nuget`).
**Working branch:** `main` (v1.3.0 published; v1.4.0 changes accumulating under CHANGELOG `[Unreleased]`).
**Build + test gate on `main`:** clean — 0 warnings, 0 errors; 1163 tests passing, 8 skipped (Cosmos emulator), 0 failed across 24 test projects.

### What's already in `[Unreleased]` (landed on `main`)

- **Phase 25 — Storefronts core services** (merged 2026-05-19): `DefaultStorefrontCartService` + `DefaultStorefrontCheckoutService` + `DefaultStorefrontCustomerService` + `GuestSessions` package with signed-cookie roundtrip + middleware. Idempotency cache, cart expiry, guest-cart promotion. 89 tests across two new test projects. DocFX article + Implementation Narrative. See `CHANGELOG.md [Unreleased]` and `TASKS.md TASK-V14-001`.
- **Phase 20 — Wallet event store** (merged 2026-05-20 as PR #4): `PolarSharp.PrepaidWallets.Abstractions` + `PolarSharp.PrepaidWallets` core domain (aggregate, handlers, behaviors, in-memory stores) + EF Core / Marten event-store providers. Funding-source provenance (`FundingSourceKind` enum + `FundingSourceAllocation` array on debits) per the WTR coordination requirement (see D-003 in DECISIONS.md). Tenant_id indexes on EF migrations. 124 tests across 4 wallet test projects. DocFX article + Implementation Narrative. See `TASKS.md TASK-V14-002`. **CHANGELOG entry pending — see TASK-V14-004.**

### Next concrete actions (this sub-cycle)

1. **TASK-V14-004 — Land Phase 20 entry in `CHANGELOG.md [Unreleased]`** before any v1.4.0 tag. The wallet work is on `main` but isn't yet documented in the release-notes file.
2. **TASK-V14-003 — Phase 26 pipeline stages**: replace the 17 log-and-pass-through skeletons in `PolarSharp.EcommerceStorefronts.Pipelines.{OrderProcessing,SubscriptionBilling,RefundProcessing}` with real implementations. `StorefrontScaffoldDiagnosticService` flags these at startup; closing them removes the last `LogLevel.Warning` from a clean-host boot. Scope, per-stage unit tests, and one end-to-end checkout pipeline test in TASKS.md.
3. **Phase 22 (Wallet bridges)** + **Phase 22.5 (WTR framework)**: scheduled after Phase 26 stabilises. Phase 22.5 is the consumer of Phase 20's funding-source provenance; design captured in the WTR section below (unchanged).
4. **Phase 23 (v1.4.0 doc sweep)** + **v1.4.0 tag**: release-artifact phase mirroring the v1.3.H pattern — refresh package READMEs, expand DocFX article coverage for the new packages (Storefronts subset still has gaps — see PROGRESS.md audit), update the CHANGELOG with both Phase 25 + Phase 20 + Phase 26 sections, version-bump the v1.4.0 family, tag + push.

The original v1.4.0 design content (test-app refresh, EcommerceStorefronts WebComponents catalog with ~85 components + theming + auth + distribution, Phase 1/2/3 sub-phase breakdown) is preserved verbatim further below in this PLAN.md under "## v1.4.0 — Test App Refresh + EcommerceStorefronts WebComponents". Don't relitigate those decisions; they're locked.

### Next major items — must address before moving on to other things

The 2026-05-20 testing overhaul produced an honest accounting of state. The following items must be fully implemented + fully tested before tagging v1.4.0 or advancing to v1.5+ work. The user's standing direction: "I need guarantees that those are working fully and fully tested as well."

1. **TASK-V14-003 (Phase 26 pipeline stages — 17 stubs)** — `PolarSharp.EcommerceStorefronts.Pipelines.{OrderProcessing,SubscriptionBilling,RefundProcessing}` ship 17 stages that are currently log-and-pass-through stubs. Cart/checkout downstream is non-functional. Real implementations + per-stage unit tests + one end-to-end checkout pipeline integration test (driving a realistic order through the full pipeline).
2. **TASK-V14-007 (AI translation provider credentials + full live tests)** — currently 5 SkippableFact-gated live tests in `LiveProviderIntegrationTests.cs` (Anthropic / OpenAI / Azure / Gemini / Grok). User MUST acquire valid credentials for all five and add them to the CI GitHub Actions secrets (`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `AZURE_OPENAI_API_KEY` + `AZURE_OPENAI_ENDPOINT` + `AZURE_OPENAI_DEPLOYMENT`, `GEMINI_API_KEY`, `GROK_API_KEY`). Once received, drop the SkippableFact gates and convert to always-running live verifications. Until then, the AI provider HTTP wire contracts are not verified in CI.
3. **TASK-V14-008 (Wallet EventStore EFC provider implementations — 5 scaffolds)** — `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.{Sqlite, SqlServer, PostgreSQL, MariaDb, CosmosDb}` are currently registration-only scaffolds (the extension methods are no-ops; XML doc says "full DbContext + migrations land in Phase 21.x"). Implement the DbContexts, migrations, provider-specific quirks (MariaDb history-repo workaround, Cosmos partition key, SQLite per-tenant file isolation), plus Testcontainers-backed per-provider integration tests covering migration idempotency + append/load + cross-tenant isolation.
4. **TASK-V14-009 (Wallet Polar bridges — 4 scaffolds)** — `PolarSharp.PrepaidWallets.Polar.{Checkout, GraphQL, Identity, Reporting}` are 25–28 line scaffold bridge files with no real implementation. Wallet has zero Polar.sh interop today. Phase 22.x lands `PolarWalletCheckoutInterceptor`, `PolarWalletRefundConverter`, `PolarWalletSubscriptionDebitor`, identity wiring (`ICurrentUser → IWalletIdentityProvider`), reporting integration + audit log `SaveChangesInterceptor`, GraphQL type extensions. Each gets per-feature tests against the live Polar sandbox.
5. **TASK-V20-008 / V20-012 (RLS DDL in migrations — cross-tenant DB-layer isolation)** — `TenantAwareDbContextBase.cs` documents a "layer 2 defense" via SQL Server / PostgreSQL Row-Level Security policies that doesn't exist in the migrations today. The only line of defense is the EF query filter (layer 1). Critical security gap. Implement raw-SQL DDL via `migrationBuilder.Sql(...)` for SqlServer (`CREATE SECURITY POLICY`) and PostgreSQL (`ENABLE ROW LEVEL SECURITY + CREATE POLICY`) per `ITenantOwned` table. Add cross-tenant raw-SQL bypass regression tests proving RLS blocks at the DB layer.
6. **TASK-V20-019 (Webhook payload capture + offline analyzer)** — current webhook tests use synthetic locally-signed payloads; we have no proof that the JSON Polar actually sends matches what our `WebhookXxxData` records expect. Polar can add fields, deprecate fields, ship new event types, or change enum values and our tests stay green. Implement `IWebhookPayloadCapture` interface + file-system capture + offline analyzer over the captured corpus + CI pre-publish gate. Privacy invariants per the design doc (opt-in, PII redaction, fingerprint-only mode, retention controls).

7. **TASK-V14-010 (Wallet read-model projections)** — surfaced 2026-05-20: the wallet event store has aggregate-internal projections + snapshots but ZERO read-model projections. Any "wallet transaction history" / "tenant wallet revenue this quarter" query forces full event-stream replay + materialization. Build `WalletBalanceSummary`, `WalletTransactionHistory`, `TenantWalletRevenueLedger` (the source-of-truth for WTR tax reports per D-002), `CustomerLifetimeWalletValue` projections via Marten's projection daemon + an EF Core polling replay equivalent. **Blocks Phase 22.5 WTR framework.**

8. **TASK-V14-011 (Gift cards as their own aggregate)** — current treatment is incomplete: gift cards are stamped as a wallet `FundingSourceKind` (`GiftCardActivation`) when redeemed but have no independent lifecycle history. Promote to a full `GiftCardAggregate` with its own event stream covering activation, partial redemption, transfer, expiry, escheatment, replacement. Wallet events reference the gift card by id; the card's own history is queryable independently. Required for accurate outstanding-gift-card-liability accounting + unclaimed-property compliance. See DECISIONS.md D-008.

9. **TASK-V14-012 (Loyalty + referrals as event-sourced aggregates)** — same problem shape as gift cards: long-lived balance-tracking things currently smushed into wallet funding-source stamps. Promote each to its own aggregate family with tier progression / attribution events / read-model projections. Unblocks the loyalty + referral WCs in the Phase 3 catalog.

Acceptance bar across all six: fully implemented + fully tested (unit + integration where applicable) + documented in CHANGELOG / DocFX articles / READMEs / Implementation Narratives + scaffold integrity test passes + no SkippableFact gates remaining for items where credentials/infra are now available.

### Lower-priority audit findings (after the above)

Tracked here so they aren't lost, but not blocking v1.4.0 tag:

- **CS1591 opt-outs in 35 packages** — `Directory.Build.props` mandates CS1591 as a build-error, but 35 packages (mostly the Storefronts family) suppress it via `<NoWarn>CS1591</NoWarn>`. Recommended approach: drop the suppression on one representative package (`PolarSharp.EcommerceStorefronts.AspNetCore`), add docs to satisfy the gate, propagate the recipe across the remaining 34 in a dedicated docs-sweep phase.
- **65 src packages with no paired test project** — many are now legitimately scaffolds (caught by `ScaffoldIntegrityTests`), but some real packages still need test coverage (`PolarSharp.GraphQL.Client` once filled in, individual EFC provider tests beyond the wallet set).

---

## Recent releases — v1.3.0 (shipped 2026-05-13)

v1.3.0 closed the v1.2.0 "interface-only" gap by shipping concrete implementations for `IRefundService`, `ILicenseKeyValidator`, `IPolarCatalogPublisher`, `IPolarBusinessProfileService`, `IInventoryUpdater`, `IPolarCatalogReader`, `ITranslationProviderResolver`, `ICatalogRepository`, `ITranslationRepository`, `IReportSnapshotService`, plus the `AddPolarEcommerce()` + `AddPolarReporting()` orchestrator extensions, plus 12 advanced reports (8 tenant-scoped + 4 SaaS-operator cross-tenant). PolarSharp / Webhooks / MultiTenant 1.2.1 → 1.3.0. See `CHANGELOG.md [1.3.0]` for the full content list and `TASKS.md` § "v1.3.0 — Missing Service Implementations ✅ SHIPPED" for the closed task list.

**Honest deferrals still open after v1.3.0** — flagged in CHANGELOG: the new services' HTTP boundaries call deferred-stub Polar adapters (TASK-V20-001..006 remain v2.0 work). v1.3.0 is feature-complete at the abstraction + orchestration layer; the live Polar HTTP plumbing ships in v2.0.

---

## Architectural framing — Polar.sh as Merchant of Record (MoR)

Added 2026-05-20 after a code review surfaced this framing had never been written down despite being foundational. Every future doc, narrative, agent prompt, and architectural decision must respect it.

**Polar.sh operates as a Merchant of Record (MoR).** For every transaction that flows through Polar's checkout — whether a one-time purchase, a subscription renewal, or a refund — Polar.sh:

1. Calculates the correct sales tax / VAT / GST for the customer's jurisdiction
2. Collects that tax from the customer at checkout
3. Files the appropriate tax returns with the relevant tax authorities
4. Remits the collected tax to those authorities on behalf of the merchant

The tenant (the actual merchant whose products are being sold) does NOTHING for tax compliance on those transactions. This is one of the primary value propositions of using Polar.sh — merchants offload the most painful operational burden of e-commerce.

**The implication for PolarSharp's architecture.** PolarSharp does NOT need to ship a tax-computation provider for the standard Polar-checkout path. The work has already been done by Polar. Shipping a TaxJar / Avalara binding "as a default" implies merchants need to think about tax, which contradicts Polar's value prop and would confuse users into installing unnecessary infrastructure.

**The exception — when MoR coverage doesn't apply.** Three scenarios bypass Polar's MoR coverage and create tax obligations PolarSharp must help its users handle:

1. **Wallet-only customer checkout** (per `PolarSharp.PrepaidWallets.Polar.Checkout`'s `PolarWalletCheckoutInterceptor` "wallet-only" mode): the end customer's purchase is satisfied entirely from their prepaid wallet balance; Polar.sh never sees the transaction; the **tenant** becomes the legal taxpayer for that sale.
2. **Hybrid checkout** (same package's "hybrid" mode): the wallet covers part of the purchase, Polar covers the rest. Polar handles MoR for its slice; the **tenant** is taxpayer for the wallet slice.
3. **Settlement Mode D — TenantPrefundedWallet** (per PrepaidWallets amendment 6 for SaaS-revenue collection): the SaaS provider's per-operation cuts are debited from each tenant's tenant-wallet in real-time rather than invoiced separately; for the SaaS's own income recognition + sales-tax obligations on its SaaS services, the **SaaS provider** must track revenue independently of Polar's MoR coverage.

These three scenarios are addressed by the **Wallet Tax Responsibility (WTR) framework** specified in the next section.

**Cross-references**:
- Inline XML doc on `IPolarBusinessProfileService.BuildBankingSetupDeepLink` already establishes that PolarSharp does not talk to Stripe; this MoR framing is the parallel principle for tax.
- The "Banking and payouts" section in `docs/articles/ecommerce-catalog.md` already documents the analogous Stripe Connect handoff; tax framing belongs in the wallet article(s).

---

## Wallet Tax Responsibility (WTR) framework — v1.3.x / v1.4.x landing

Designed 2026-05-20 after a user-surfaced question about TaxJar's role in v1.4.0. Single framework that covers BOTH actor perspectives (tenant-perspective wallet-only sales tax + SaaS-perspective per-tenant revenue tax) using the same primitives, because the architectures are isomorphic (one level apart in the actor hierarchy).

### The two-actor model

The same `WalletDebited` event is viewed from two perspectives depending on who's asking:

| Perspective | The wallet | The debit | Tax-relevant party | When MoR coverage gap exists |
|---|---|---|---|---|
| **Tenant** | End customer's wallet at a tenant marketplace | End customer pays for a product/service | **Tenant** (the marketplace operator) | Wallet-only customer checkout OR hybrid (wallet slice) |
| **SaaS** | Tenant's tenant-wallet with the SaaS provider | SaaS deducts its per-operation cuts | **SaaS provider** | Settlement Mode D (TenantPrefundedWallet) |

**Both perspectives use the same wallet event stream** — different aggregations, different jurisdictional rules, different reports. ONE framework, two views.

### Five components

#### Component 1 — Acknowledgment (hard gate, not dismissable popup)

Two flows:

- **Tenant acknowledgment** when enabling wallet-only or hybrid checkout for their marketplace. Captured to `wallet_tax_acknowledgments` (per-tenant table) with timestamp + actor user + acknowledged-jurisdiction list. Re-acknowledgment required when tenant adds operations in a new jurisdiction.
- **SaaS acknowledgment** at PolarSharp installation (regardless of settlement mode) AND at the moment Settlement Mode D is first enabled for any tenant. Captured to `saas_tax_acknowledgments` (non-tenant-scoped; SaaS-only).

Both flows have a "snooze 7 days to talk to my accountant first" option that keeps the feature DISABLED until acknowledged.

#### Component 2 — Funding-source-aware estimated tax calculators

Two estimator services sharing one rate-table backbone:

- **`TenantTaxEstimator`** — computes tenant's tax owed on wallet-only / hybrid customer debits. Reads `FundingSources` allocations on each `WalletDebited` event (per the Phase 20 coordination note that added funding-source provenance to wallet event signatures). Applies tax treatment per source kind (e.g. `CustomerCashFunded` = fully taxable, `TenantPromotionalGrant` = discount-reduces-basis by default; jurisdiction-configurable).
- **`SaaSTaxEstimator`** — computes SaaS's tax owed on per-tenant revenue across all settlement modes. Differentiates revenue types (subscription / funding-cut / transaction-cut / maintenance-fee-cut / refund-surcharge-cut) and per-tenant jurisdiction for sales-tax nexus analysis.

Both estimators ship a `BasicEstimatedTaxCalculator` default with a quarterly-updated static rate table covering:
- Every US state's sales tax rate (50 + DC)
- Every US state's corporate income tax rate (where applicable; 8 states have no corporate income tax)
- EU VAT standard rates per member country (27 countries)
- GST rates for AU, NZ, CA, IN, SG, etc.
- US federal corporate income tax rate (21% flat post-TCJA)

Labeled "estimate only — not filing-grade" in every output. Tenants/SaaS needing certified accuracy install a real `IStorefrontTaxProvider` (or analogous `ISaaSTaxProvider`) implementation.

#### Component 3 — Comprehensive tax-owed reports (BOTH quarterly + annual + accumulative)

Per the user's 2026-05-20 expanded ask: each report shows period-to-date accumulation, full-period projection (PTD run-rate × time-remaining), prior-period comparison for trend visibility, and breakdowns by tax TYPE not just jurisdiction.

##### `TenantTaxOwedReport(tenantId, asOfDate)`

Returns a hierarchical shape:

```
CurrentQuarter:
  Label: "Q2 2026"
  StartDate: 2026-04-01, EndDate: 2026-06-30, DaysElapsed: N, DaysRemaining: M
  EstimatedPaymentDeadline: 2026-06-15
  DaysUntilDeadline: K

  PTD (period-to-date, through asOfDate):
    TaxableSales: $X
    TaxOwed:
      Federal:          { Type: "income_tax",     EstimatedOwed: $X }
      State:            [
        { Code: "US-CA", Type: "sales_tax",  TaxableBase: $X, RateBps: 887, EstimatedOwed: $X },
        { Code: "US-CA", Type: "income_tax", TaxableBase: $X, RateBps: 884, EstimatedOwed: $X },
        { Code: "US-NY", Type: "sales_tax",  TaxableBase: $X, RateBps: 800, EstimatedOwed: $X },
        ...
      ]
      Foreign:          [
        { Code: "DE-VAT", Type: "vat",       TaxableBase: $X, RateBps: 1900, EstimatedOwed: $X },
        { Code: "AU-GST", Type: "gst",       TaxableBase: $X, RateBps: 1000, EstimatedOwed: $X },
        ...
      ]
      GrandTotal: $X

  Projected (full quarter, based on PTD run-rate):
    [same structure as PTD]

CurrentYearToDate:
  Label: "2026"
  YearStart: 2026-01-01, AsOfDate, DaysElapsed: N
  AnnualReturnDeadline: 2027-04-15

  YTD:    [same TaxOwed structure as PTD]
  Projected (full year): [same]

PriorYearSamePeriod:
  Label: "2025 (through May 20)"
  TaxOwed: [same structure]   # comparison context

QuarterlyTrend:
  [Q1 2026: total $X, Q2 2026 PTD: $Y, Q3 2026: pending, Q4 2026: pending]

FundingSourceBreakdown:
  CustomerCashFundedTaxable: $X            # fully taxable basis from cash-funded debits
  GiftCardActivationTaxable: $X            # fully taxable basis from gift-card redemptions
  RefundAsCreditTaxable: $X                # fully taxable basis from refund credits
  PromotionalGrantBasisReduction: -$X      # how much the rewards/promo grants reduced taxable basis
  ... per FundingSourceKind ...

Disclaimer: "These are estimates based on published rates as of each transaction's date. Not certified for filing. Consult a licensed tax professional in each listed jurisdiction before filing or remitting."
```

CSV / JSON / Excel export. Per-tenant scoped (Audience tier: Tenant operator + SaaSAdmin with `[AllowCrossTenant]` only).

##### `SaaSTaxOwedReport(asOfDate)`

Same hierarchical shape, scoped to the SaaS provider's revenue:

```
CurrentQuarter:
  Label: "Q2 2026"
  ... (same date dimensions)
  EstimatedPaymentDeadline: 2026-06-15      # IRS Form 1120-W federal quarterly deadline

  PTD:
    Revenue:
      Subscription:           $X         # ongoing $/month from each tenant
      FundingCut:             $X         # 5% (default) cuts on wallet funding operations
      TransactionCut:         $X         # 4.75% on non-prepaid Polar transactions
      MaintenanceFeeCut:      $X         # 50% of dormancy fees
      RefundSurchargeCut:     $X         # 50% of refund surcharges
      GrandTotal:             $X

    EstimatedExpenses: $X                 # configurable; default: industry-typical 20% margin assumption
    EstimatedNetProfit: $X                # Revenue - EstimatedExpenses (for income-tax basis)

    TaxOwed:
      Federal:    { Type: "corporate_income_tax", EstimatedRate: 21%, EstimatedOwed: $X }
      State:      [
        { Code: "US-DE", Type: "corporate_income_tax", Basis: $X, RateBps: 870, EstimatedOwed: $X },
        # only states where SaaS has nexus AND tax applies to corporate income
      ]
      SalesTaxOnSaasServices: [
        # only states where SaaS has nexus AND state taxes SaaS services
        { Code: "US-NY", Type: "sales_tax_on_saas", Basis: $X, RateBps: 800, EstimatedOwed: $X },
        ...
      ]
      Foreign: [
        # if SaaS bills EU tenants, B2B reverse-charge mechanism may apply
        { Code: "DE-VAT", Type: "vat_on_saas", Basis: $X, RateBps: 1900, EstimatedOwed: $X, ReverseChargeApplies: true },
        ...
      ]
      GrandTotal: $X

  Projected (full quarter): [same structure]

CurrentYearToDate: [same]
PriorYearSamePeriod: [same]
QuarterlyTrend: [same]

PerTenantRevenueBreakdown:
  # which tenants generated which revenue in this quarter
  - TenantId, TenantName, TenantJurisdiction, Revenue:$X, SettlementMode
  - ... one row per tenant
  # for nexus analysis: where are revenue-contributing tenants located?

PerSettlementModeBreakdown:
  StripeConnect:               $X    # SaaS cut routed via application_fee_amount at point of charge
  BundledMonthlyInvoice:       $X    # cuts settled via monthly Polar invoice to tenant
  StandalonePolarOrder:        $X    # cuts settled via separate Polar Order
  TenantPrefundedWallet:       $X    # cuts deducted from tenant-wallet in real-time (THE one needing the most attention)

Disclaimer: "Estimates only. Federal estimated tax payments due quarterly per IRS Form 1120-W schedule. State deadlines vary. Consult a licensed CPA before filing."
```

Audience tier: SaaSAdmin only (with `[RequirePolarPermission(ViewSaasTaxReport)]`).

Both reports support these output formats: CSV, JSON, Excel (multi-sheet workbook with per-jurisdiction tabs), PDF (for handoff to accountant).

#### Component 4 — Quarterly deadline reminder service

`SaaSTaxDeadlineReminderService` IHostedService (and `TenantTaxDeadlineReminderService` analog):
- Knows each jurisdiction's tax calendar (IRS federal quarterlies: Apr 15, Jun 15, Sep 15, Jan 15; state deadlines vary; EU VAT typically monthly or quarterly)
- 21 days before each deadline: sends a Notice notification to billing contacts: *"Q2 federal estimated tax deadline is Jun 15. Your YTD revenue is $X with estimated quarterly federal tax of $Y. View report."*
- 7 days before: sends a Warning escalation
- 1 day before: sends an Urgent escalation
- Uses the same wallet notification dispatcher as the v1.3 amendment 3 framework — no parallel infrastructure
- Tenant escalation policy + SaaS escalation policy both honor recipient preferences

#### Component 5 — Settlement-mode-aware revenue ledger

`saas_revenue_ledger` (non-tenant-scoped table; SaaS owns it):

```
saas_revenue_ledger
  id                       UNIQUEIDENTIFIER PK
  tenant_id                UNIQUEIDENTIFIER         -- which tenant generated this revenue
  tenant_jurisdiction      NVARCHAR(8)              -- tenant's primary jurisdiction (for SaaS nexus analysis)
  revenue_type             NVARCHAR(32)             -- subscription | funding_cut | transaction_cut | maintenance_fee_cut | refund_surcharge_cut
  settlement_mode          NVARCHAR(32)             -- StripeConnect | BundledMonthlyInvoice | StandalonePolarOrder | TenantPrefundedWallet
  amount_cents             INT
  currency                 NVARCHAR(3)
  recognized_at_utc        DATETIME2                -- revenue-recognition date for tax purposes (accrual basis)
  collected_at_utc         DATETIME2 NULL           -- when SaaS actually received cash (cash basis); may differ
  related_wallet_event_id  UNIQUEIDENTIFIER NULL    -- when sourced from a wallet debit
  related_polar_order_id   NVARCHAR(64) NULL        -- when sourced from a Polar order
  related_invoice_id       NVARCHAR(64) NULL        -- when sourced from a bundled invoice
  -- indexes:
  --   ix_recognized_at_utc (recognized_at_utc DESC)            for time-range queries
  --   ix_tenant_id (tenant_id, recognized_at_utc DESC)         for per-tenant queries
  --   ix_revenue_type (revenue_type, recognized_at_utc DESC)   for revenue-type breakdown queries
```

Source of truth for the SaaS tax reports. Every settlement mode writes here uniformly; reports aggregate over this ledger without provider-specific code paths.

### Tax-type matrix (what's computed for each tax type)

| Tax type | Estimator inputs | Computation | Who owes |
|---|---|---|---|
| **US state sales tax** | jurisdiction code, taxable basis (from FundingSources × treatment rules) | basis × per-state rate | Tenant (wallet-only sales) / SaaS (if state taxes SaaS services + SaaS has nexus) |
| **US state corporate income tax** | jurisdiction code, estimated net profit (Revenue - estimated expenses) | net profit × per-state corporate rate | SaaS (where it has nexus) |
| **US federal corporate income tax** | estimated net profit | net profit × 21% | SaaS |
| **EU VAT** | EU country code, taxable basis, B2B-vs-B2C indicator (reverse-charge rules) | basis × per-country VAT rate; zero for B2B reverse-charge | Tenant (wallet-only EU sales) / SaaS (if billing EU tenants outside B2B reverse-charge scope) |
| **GST (AU/NZ/CA/IN/etc.)** | country code, taxable basis | basis × per-country GST rate | Tenant (wallet-only sales in those countries) / SaaS (if billing into those countries) |

### TaxJar binding — decision (revised 2026-05-20 after PR #3)

Initial framing (during the design discussion earlier 2026-05-20): remove the concrete TaxJar binding to eliminate the suppressed RestSharp vulnerability + the maintenance burden. The user picked Option B (keep abstraction, remove binding).

**Revised framing** after PR #3 landed (`a9170c1`, "chore(cpm): override transitive RestSharp to 114.0.0 to lift CI vuln gate"):
- The user enabled `CentralPackageTransitivePinningEnabled=true` in `Directory.Packages.props` and pinned `RestSharp` to `114.0.0` as a transitive override. This satisfies TaxJar 4.0.0's `RestSharp >= 108.0.3` floor while lifting the codebase out of GHSA-4rr6-2v9v-wcpc (CRLF injection affecting RestSharp versions in [107.0.0-preview.1, 112.0.0)).
- TaxJar 4.0.0 + RestSharp 114.0.0 (transitively pinned) now ships safely; no CI vuln-scan failures.
- `src/PolarSharp.EcommerceStorefronts.Tax.TaxJar/` remains `IsPackable=false` with zero hand-written source files — it's a scaffold awaiting the v1.4.0 integration work.

**Net decision (as of 2026-05-20)**:
- **Keep** the `IStorefrontTaxProvider` abstraction (unchanged).
- **Keep** the TaxJar scaffold package; do NOT delete it. The vuln-driven rationale for removal is gone. The package can be filled in during v1.4.0 if WTR's `BasicEstimatedTaxCalculator` + bring-your-own-implementation pattern proves insufficient for the wallet-only mode use case.
- **Re-evaluate** the pin (and whether to actually implement TaxJar vs only ship the abstraction) when v1.4.0's TaxJar integration is in flight. The decision then depends on whether real-world tenant demand for a concrete TaxJar binding materializes.
- WTR's `BasicEstimatedTaxCalculator` covers the ballpark-figure case for free; tenants needing certified accuracy can install whichever third-party tax provider they prefer (TaxJar, Avalara, Stripe Tax, custom) via the `IStorefrontTaxProvider` seam.

### Phase plan

WTR ships as a new phase between PrepaidWallets bridges (Phase 22) and v1.3 doc sweep (Phase 23):

- **Phase 20** (current — wallet agent's active work): adds funding-source provenance to wallet event signatures per the coordination note. NO tax computation logic.
- **Phase 21** (per-provider EF Core wallet event stores): no WTR-specific work; tenant_id indexes need to be present (per the coordination note).
- **Phase 22** (Polar bridges for wallet): no WTR-specific work.
- **Phase 22.5 (NEW) — WTR framework implementation**:
  - Estimated 25-40 source files + 25-40 tests
  - 1 DocFX article: `docs/articles/wallet-tax-responsibility.md`
  - 2 Implementation Narratives:
    - `docs/narratives/understanding-tax-when-you-use-wallet-only-checkout.md` (audience: tenant operators)
    - `docs/narratives/saas-tax-tracking-and-quarterly-reporting.md` (audience: SaaS deployers)
  - Per-package READMEs
  - Lift-shift posture: WTR's tenant-perspective tooling sits in `PolarSharp.PrepaidWallets.Tax.*` (lift-safe core); SaaS-perspective tooling sits in `PolarSharp.PrepaidWallets.Polar.Tax.*` bridge (couples to PolarSharp.MultiTenant.Identity for SaaSAdmin authz)
  - Audience-tier respecting GraphQL exposure: SaaS report only visible to SaaSAdmin; tenant report visible to tenant operators
- **Phase 23** (existing v1.3 doc sweep): no WTR-specific work; the docs agent's current scope doesn't include WTR

### Open question — escalation posture (deferred)

When a tenant or the SaaS has growing estimated unfiled tax and hasn't generated a report in N days, what's the appropriate platform response?

| Option | Posture | What it does |
|---|---|---|
| **A — passive** | Least paternalistic | Surface the info; tenant decides |
| **B — nudge** | Middle | Send escalating reminders as estimated unpaid tax grows; gentle nags via notification system |
| **C — gate** | Most protective | Auto-disable wallet-only mode if estimated unfiled tax exceeds threshold AND no report generated in N days |

To be decided at Phase 22.5 design-finalization. Default working assumption: B (nudge) for both tenant and SaaS perspectives; preserve A as configurable for tenants who explicitly opt out of nudges; C is too paternalistic for v1 but may be added later as an opt-in safety mode.

---

## v1.4.0 — Test App Refresh + EcommerceStorefronts WebComponents

**Status:** design phase (2026-05-19); implementation scheduled after v1.3.0 ships.
**Scope summary:** Three parallel deliverables — (Phase 1) PolarTestApp endpoint refresh covering every Polar customer journey; (Phase 2) `PolarSaasDemo` Blazor flagship + Blazor/MAUI internal-UI component packages for SaaS/admin/merchant demo apps; (Phase 3) the substantial new `PolarSharp.EcommerceStorefronts.WebComponents` catalog + theming + auth + distribution work designed during the 2026-05-19 architecture session, providing embed-anywhere customer-facing Web Components for the army-of-marketplaces tenant deployment model.

After v1.3.0 ships, the planned v1.4.0 work:

- **Phase 1:** Enhance `testapp/PolarTestApp` with ~14 new endpoint files covering every Polar customer journey across all 31 packages, with the seeding flow demonstrating end-to-end seed → publish → webhook callback → drilldown
- **Phase 2:** New `PolarSaasDemo` flagship app — **Blazor Web App template with `RenderMode.InteractiveServer`** (the modern .NET 8+ unified hosting model with SignalR-powered interactivity, NOT "Blazor Hybrid" which means MAUI/WPF embedding). Uses **Telerik UI for Blazor** components (license file `**/telerik-license.json` gitignored; documented setup step in README; CI uses `TELERIK_LICENSE_KEY` secret). Real-time UI driven by `IPolarToastChannel` (already in `PolarSharp.Webhooks`) bridged to a SignalR hub: webhook arrives at the host → channel publishes → SignalR pushes to subscribed circuits → Telerik grid invalidates the affected slice and re-renders within hundreds of milliseconds, so the UI always reflects the latest Polar state without manual refresh. Telerik's Blazor MCP server (`mcp__telerik-blazor-assistant__telerik_blazor_assistant`) is already configured in the environment for component generation, theming, and `TelerikGrid` + `DetailTemplate` scaffolding aligned to the Progress Design System. Demonstrates the full multi-tenant SaaS narrative: onboard tenant → seed catalog → publish to Polar → receive webhooks (live in the UI) → view reports → manage users → audit changes.

  **UI architecture — reusable modular components, shipped as a three-package set covering Web and MAUI.** The reusable components do NOT live inside the demo apps — they ship as standalone NuGet packages so any Blazor or MAUI host can consume them. Decided 2026-05-13 with project owner.

  - **`PolarSharp.UI.Components.Core`** *(new NuGet package, plain class library)* — pure C# view models, state records, validator helpers, no rendering. `KpiTileViewModel`, `PermissionGateState`, `HierarchicalGridState<TParent,TChild,TLeaf>`. Both the Web and MAUI packages consume this. Targets `net10.0`.
  - **`PolarSharp.UI.Components.Web`** *(new NuGet package, Razor Class Library)* — Blazor components for Blazor Server, Blazor WebAssembly, AND Blazor Hybrid (since Blazor components are just Razor + C#, all three hosting models render them). The Web flagship demo consumes this. Independently testable via bUnit in `tests/PolarSharp.UI.Components.Web.Tests/`. Targets `net10.0`.
  - **`PolarSharp.UI.Components.Maui`** *(new NuGet package, MAUI control library)* — MAUI-specific controls: mobile-first layouts, native page templates, gesture handlers, platform-integrated features (camera, biometric, push notifications). Plus mobile-tuned variants of components where layouts genuinely differ from web (drawer navigation, gesture-driven tabs, etc.). Targets `net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0-windows*`. Reserved for v1.5.0 build-out alongside the mobile companion flagship.

  **`PolarSharp.Templates`** *(existing, EXTEND)* — keeps the existing `polar-handler` webhook scaffold. **Adds new templates**:
  - `polar-saas-demo` — Blazor Web App pre-wired to `PolarSharp.UI.Components.Web` + the v1.2.0 SDK (the v1.4.0 flagship)
  - `polar-tenant-portal` — leaner merchant-only template (web)
  - `polar-admin-console` — AppMasterAdmin-only console (web)
  - `polar-mobile-companion` *(v1.5.0)* — .NET MAUI Blazor Hybrid app pre-wired to `PolarSharp.UI.Components.Maui` + `.Web`
  - `dotnet new --list polar` becomes the discovery surface.

  **`PolarMauiDemo`** *(v1.5.0 flagship)* — full reference MAUI Blazor Hybrid app generated by the `polar-mobile-companion` template. Targets `net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0-windows*`. Demonstrates the mobile-merchant companion narrative: view orders, get push-notification alerts on new orders, scan barcodes for inventory, biometric-unlock the audit log, offline-cached drilldown reports. Ships as a `testapp/` reference implementation alongside `PolarSaasDemo`; not itself a published NuGet package. Required for any meaningful adoption of the MAUI RCL — without a working sample, consumers have no concrete example of wiring the components together. **Local-dev setup documentation is a hard requirement** for v1.5.0: the DocFX `local-development.md` article gains a dedicated "Setting up .NET MAUI for PolarSharp development" section covering workloads (`dotnet workload install maui-android / maui-ios / maui-maccatalyst / maui-windows`), per-OS tooling (Xcode on macOS, VS2026 + Android SDK on Windows), simulators / emulators, signing certs + provisioning profiles, Telerik MAUI licensing notes, and a "first-run validation" command sequence (`dotnet new polar-mobile-companion && dotnet build -f net10.0-android`). See `CLAUDE.md` documentation-standards section for the binding writing requirements.

  **Flagship demo scope decision** (locked 2026-05-13): `PolarSaasDemo` is **web-only** (Blazor Web App with `RenderMode.InteractiveServer`) for v1.4.0. A MAUI mobile companion app ships separately in v1.5.0 once the `PolarSharp.UI.Components.Maui` package is built out. This keeps Phase 2 of v1.4.0 focused on the most common use case while still committing to the multi-platform component architecture from day one.

  **Why this three-package split** (locked decision; see also `CLAUDE.md` documentation-standards section):
  - Template packs use `<PackageType>Template</PackageType>` and ship `dotnet new` metadata, not compiled runtime code; an RCL ships compiled `.razor` components via standard `<PackageReference>`. Mixing them would break both consumption mechanisms.
  - Web (Blazor) and MAUI components have fundamentally different concerns at the edges — navigation, live-data transport (SignalR for web vs. push notifications for mobile), auth flows, native capabilities, layout sizing. Forcing them into one package creates "don't use this here" warnings; splitting them makes the boundary explicit at the package level.
  - The `.Core` package captures everything that's hosting-model-agnostic (view models, state records, options) — both Web and MAUI consume it, so cross-platform reusability is built into the architecture from day one without compromising either platform's idioms.
  - Each package gets its own version cadence and test surface; updating the mobile components doesn't churn the web components and vice versa.

  Composition principles: one concern per component, parameters in / `EventCallback<T>` events out, generics where the shape varies, strict separation of presentation from data fetching (components take data and callbacks; they never resolve services directly except for cross-cutting concerns like the toast channel). Pages compose components rather than rendering markup directly.

  Planned reusable components (initial set; will grow as needed during P2.B–P2.D):
  - **`PolarKpiTile`** — single metric with label, value, optional trend % and icon. Used on revenue dashboard, customer dashboard, audit dashboard.
  - **`PolarStatusBadge`** — colored badge mapping `PublishStatus` / `OnboardingStepKind` / `PayoutSetupStatus` to consistent visual treatment.
  - **`PolarHierarchicalGrid<TParent, TChild, TLeaf>`** — generic wrapper around `TelerikGrid` + `DetailTemplate` for the three-level drilldown pattern. Takes `Func<...> LoadParents`, `Func<TParent, ...> LoadChildren`, `Func<TChild, ...> LoadLeaf` delegates so it works for Customer→Order→OrderDetail AND for any future Tenant→Membership→AuditTrail or Department→Category→Product drilldowns.
  - **`PolarLiveToastSubscriber`** — wraps the SignalR subscription to `IPolarToastChannel`; exposes `EventCallback<ToastEvent>` for parent components to react. Drops onto any page that needs live updates.
  - **`PolarTenantContextBar`** — current-tenant indicator + (for AppMasterAdmin) tenant switcher + cross-tenant-mode toggle.
  - **`PolarPermissionGate`** — `<PolarPermissionGate Required="PolarPermission.EditCatalog">…</PolarPermissionGate>` for declarative RBAC inside markup. Hides content when current user lacks the permission. Pairs with the existing `[RequirePolarPermission]` attribute on endpoints.
  - **`PolarAuditTrailList`** — paginated audit-log list with filtering by actor / entity-type / date range. Used by tenant admins for their own log and by AppMasterAdmins for the platform-wide log.
  - **`PolarOnboardingWizardStep`** — base component for each wizard step (CompanyBasics, ProductTypes, WebhookConfig, etc.) — handles the common "submit → wait → show next-step or errors" flow.
  - **`PolarPublishStatusBanner`** — for catalog publish state with action buttons (Preview, Publish, View errors).
  - **`PolarTranslationKeyInput`** — secure single-use input for per-tenant translation API key (encrypts client-side via Data Protection before transmit, never shows plaintext after submission).
  - **`PolarRefundDialog`** — typed refund issuance dialog (full / partial, reason codes, comments) used from order detail rows.

  Pages compose these. Example: the merchant dashboard page becomes a layout with `<PolarTenantContextBar />` + four `<PolarKpiTile />`s wired to reporting queries + `<PolarHierarchicalGrid TParent="CustomerListRow" TChild="OrderSummaryRow" TLeaf="OrderDrilldownDetail" />`. No bespoke markup — the page is ~50 lines of Razor over reusable component instances.

  Telerik MCP usage will lean on this pattern: component generation requests are at the **component** level, not at the page level. Each `PolarXxx*` component encapsulates the Telerik markup + styling alignment; pages stay free of Telerik-specific syntax.

See the conversation log for the full Phase 1 / Phase 2 sub-phase breakdown agreed with the project owner.

### Phase 3 — EcommerceStorefronts WebComponents catalog + theming + auth + distribution (designed 2026-05-19)

**Status:** design locked across 8 batches of decision-making during the 2026-05-19 architecture session; implementation scheduled to start once v1.3.0 ships + Phase 1 + Phase 2 are underway. Detail tracked in tasks #72 through #89 + #87 (SSO).

**Why this exists.** The project owner's deployment model is the **army of trending-specific marketplaces** — many ("hundreds of") tenant marketplaces, each in a different niche (vintage keyboards, fountain pens, sustainable fashion, etc.), constructed at scale by **agentic AI agents** that decision-tree-select WebComponents from PolarSharp's published catalog. To support that scale + variety, PolarSharp ships a deliberately large + comprehensively documented Web Component catalog (currently scoped at ~90 WCs across v1.4.0 + v1.4.x + v1.5+) where every component carries machine-parseable documentation (frontmatter with purpose, data-source, dependencies, composes-with, conflicts-with, required-tenant-config, audience, deployment-context, decision-tree-tags) so agents can mechanically filter the catalog when generating a specific marketplace.

**What this is — and what it isn't.** Phase 3 ships `PolarSharp.EcommerceStorefronts.WebComponents` — a **Stencil-compiled Web Components bundle** for **embed-anywhere customer-facing surfaces** (the tenant's own marketplace + partner / third-party / blog / aggregator sites that surface the tenant's products inline). This is **distinct from** Phase 2's `PolarSharp.UI.Components.{Core, Web, Maui}` packages, which are Blazor + MAUI components for PolarSharp's INTERNAL flagship demo apps (`PolarSaasDemo`, future `PolarMauiDemo`) + tenant-admin / SaaS-admin / merchant-admin Razor pages. Two different deliverables for two different audiences.

**Architectural foundations (locked decisions; detail in following sub-sections).**

- **Per-tenant CSS theming via design tokens** — 30 tenant-configurable design tokens (12 colors + 6 typography + 3 spacing + 3 shape + 2 motion + 4 component-specific). Each token is either an attribute alias on every WC (8 tokens — primary, accent, border, border-radius + 4 component-specific) OR CSS-variable-only (22 tokens). New `--polar-size-scale` token maps semantic sizes (small/default/large/hero) to numeric multipliers; the `size="..."` attribute pattern handles per-element scaling cleanly across WCs. State attribute pattern ([selected], [disabled], [active], [loading], [hovered], [featured]) is the canonical way to visualize state without attribute clutter.

- **Embed-anywhere vs tenant-storefront-only WC catalog split** — WCs are classified per their deployment context: embed-anywhere (works on any partner / third-party site without account context); tenant-storefront-only (requires the tenant's own storefront context with account state). The split prevents accidentally surfacing tenant-storefront WCs (account-menu, wallet-balance, order-history-list) on partner sites where they would be confusing.

- **Auth model: anonymous-first, deferred to checkout.** Site visitors to partner sites browse + add to cart anonymously. Authentication happens inline as a step inside `polar-checkout-page` — guest checkout always available; existing-account-recognition + sign-in OR setup wizard for new accounts inline at checkout. No standalone `polar-login-button` / `polar-logout-button` WCs (confusing on partner sites with their own auth); `polar-account-menu` is the unified replacement for the tenant's own storefront.

- **Tenant-AI policy: BYOK + real-time validation, no SaaS-master fallback.** All AI-dependent WCs (review summarization, future NL search, future personalized recommendations, gift-finder wizard) require tenant-supplied validated AI provider credentials. The v1.3 translation feature's 3-tier resolution gets refactored retroactively to the same 2-state model (validated → enabled; otherwise → disabled). Consistent mental model across all AI features.

- **Per-tenant social SSO architecture.** Per-provider packages (`PolarSharp.MultiTenant.Identity.Sso.{Google, Microsoft, Facebook, Apple, GitHub, LinkedIn, X, Snapchat, Pinterest, TikTok}`). Top 4 (Google + Microsoft + Facebook + Apple) ship v1.4.0; others ship as named v1.4.x patches (e.g. v1.4.1 = GitHub + LinkedIn). Tenant BYOK with real-time validation mirroring the tenant-AI policy. New `polar-sso-button-group` WC renders the configured providers' sign-in buttons per tenant. Account-merging via auto-link-with-confirmation-prompt.

- **5-layer tenant-isolation acceptance criteria for every new entity.** Every new entity / column introduced by ANY WC backend addition must explicitly satisfy: (1) `ITenantOwned` interface + global query filter in `TenantAwareDbContextBase`; (2) RLS policy in initial EF migration for SqlServer + Postgres providers; (3) SQLite per-tenant `.db` file placement via existing `SqlitePerTenantDbContextFactory`; (4) MariaDB app-layer filter (no native RLS); (5) Cosmos `/tenantId` partition key. Plus single-tenant mode behavior (filter no-op; entity keeps TenantId column). Cross-tenant isolation regression tests required per the existing `CrossTenantIsolationTests` template.

- **Accessibility-first theme editor.** WC catalog must meet WCAG 2.2 AA as a launch baseline. Theme editor enforces this at design time — color-blindness simulator filters on the live preview (~8% of males affected), per-token WCAG contrast checker, accessible-palette suggestions, publish-gate blocking critical accessibility failures unless tenant explicitly signs off. Lock-token enforcement validates locked colors still produce WCAG-compliant contrast. Comprehensive accessibility task #89 covers screen readers, keyboard navigation, focus management, touch targets, reduced motion, high contrast, dyslexia-friendly opt-in, plus per-tenant accessibility policy on `TenantBusinessProfile`.

- **Distribution via 3 channels: NuGet + npm + CDN.** Locked semver across all three (identical artifact + same bytes). Bundle ships as both combined (~150KB gzipped for all WCs) AND per-component ES modules (tree-shaking-friendly). CDN is Cloudflare R2 + Workers (aligns with future `PolarSharp.MediaAndFileStorage` direction). URLs both pinned (e.g. `cdn.polarsharp.dev/wcs/1.4.0/...`) and floating (`/wcs/1.4/...`); pinned recommended in docs. SRI hashes auto-generated per release. CSP requirements documented per channel with copy-paste examples. CDN-unreachable handled via a built-in loader-script fallback to self-hosted URL. Tenant theme injection via SignalR hello message on bundle init; 3 bootstrap syntaxes supported (attribute / meta tag / global init function).

- **WebComponent Styling Guide narrative (required).** New top-level Implementation Narrative `docs/narratives/web-component-styling-guide.md` covering every WC with: state attribute names, CSS selector patterns for each state, common visual treatment recipes (overlay pseudo-elements, filters, blend modes), worked examples (the plan-picker selected/deselected walkthrough is the canonical example), accessibility patterns per WC (color-pairing-with-icons recipes, focus-management defaults, keyboard-shortcut conventions). Targets frontend designers (CSS-literate but not WC-internals-literate). Lands when WCs land.

#### Theming token catalog + design pattern commitments

**31 design tokens** (30 originally proposed + the new `--polar-size-scale` introduced during the typography walkthrough). Each token is either exposed as a **friendly element attribute alias** (designer can override per-instance via `<polar-product-card primary-color="#ff0000">` syntax) OR **CSS-variable-only** (designer must use `style="--polar-primary: #ff0000"` or external CSS). Attribute-aliased tokens map cleanly to inline CSS-variable-set-on-the-element via Stencil's `@Prop({ reflect: true }) + @Watch` pattern (~5 lines per token); the inline attribute wins standard CSS specificity (inline style beats stylesheet) so the precedence model is `WC bundle defaults < tenant theme (SignalR-injected at :root) < host page CSS < inline attribute`.

**Colors (12 tokens).** All carry `--polar-` prefix.

| Token | Attribute alias? | Use |
|---|---|---|
| `--polar-primary` | YES (`primary-color`) | Main brand color: CTA buttons, links, focus rings, selected highlights. Most-overridden token. |
| `--polar-accent` | YES (`accent-color`) | Secondary brand: category badges, decorative ribbons, secondary CTAs, hover-state highlights. |
| `--polar-surface` | no (CSS-var-only) | Card / panel / modal background. Tenant-wide. |
| `--polar-surface-elevated` | no | Hover / active / pressed state for cards + buttons. Derived from surface. |
| `--polar-text` | no | Body text color. Tenant-wide. |
| `--polar-text-muted` | no | Secondary text: timestamps, helper text, strikethrough prices, breadcrumb separators. |
| `--polar-text-inverse` | no | Text on dark backgrounds: CTA button labels, badge text on primary/accent. |
| `--polar-border` | YES (`border-color`) | Default borders: cards, inputs, dividers. Per-element override common for visual hierarchy (sale items get red borders, etc.). |
| `--polar-success` | no | Confirmation states: success toasts, checkmark icons, in-stock badges. |
| `--polar-warning` | no | Warning states: low-stock badges, sale-ending banners. |
| `--polar-error` | no | Error states: declined-payment toasts, form-field validation errors, destructive buttons. |
| `--polar-info` | no | Informational states: shipment-status toasts, helper-tip backgrounds. |

**Typography (6 tokens).** All CSS-variable-only — tenant-wide brand decisions; per-element variation handled via the `size` attribute pattern below.

| Token | Use |
|---|---|
| `--polar-font-family` | Primary font stack. |
| `--polar-font-family-heading` | Distinct heading font (optional; falls back to `--polar-font-family`). |
| `--polar-font-family-mono` | Monospace for SKUs, license keys, order numbers, API tokens. |
| `--polar-font-size-base` | Base font size (default 16px); every other size derives. |
| `--polar-font-weight-bold` | What "bold" means for the tenant's chosen font (600 vs 700 etc.). |
| `--polar-line-height` | Default line height (default 1.5). |

**Spacing (3 tokens).** All CSS-variable-only. Per-element variation via `size` attribute.

| Token | Use |
|---|---|
| `--polar-spacing-unit` | Base spacing unit (default 0.5rem = 8px). All paddings + margins + gaps derive. |
| `--polar-spacing-tight` | Tight variant (default = 0.5×unit). |
| `--polar-spacing-loose` | Loose variant (default = 2×unit). |

**Shape (3 tokens).**

| Token | Attribute alias? | Use |
|---|---|---|
| `--polar-border-radius` | YES (`border-radius`) | Global corner radius for cards / buttons / inputs / badges. Strong brand-identity signal; per-element override common (hero cards more rounded, clearance badges pill-shaped). |
| `--polar-border-width` | no | Default border thickness (default 1px). |
| `--polar-shadow-depth` | no | Drop shadow intensity, semantic enum `none|sm|md|lg|xl` mapped to box-shadow values internally. |

**Motion (2 tokens).** All CSS-variable-only.

| Token | Use |
|---|---|
| `--polar-animation-duration` | Base duration (default 200ms). |
| `--polar-animation-easing` | Easing function (default `cubic-bezier(0.4, 0, 0.2, 1)` = Material standard ease). |

**Component-specific (4 tokens, scoped to the WCs that use them).**

| Token | Scoped to | Attribute alias? | Use |
|---|---|---|---|
| `--polar-product-card-image-aspect-ratio` | product-card / product-grid / product-detail | YES (`image-aspect-ratio`) | Per-product variation common in mixed catalogs (fashion 3:4, electronics 1:1, lifestyle 16:9). |
| `--polar-mini-cart-position` | polar-mini-cart | YES (`position`) | Where the mini-cart docks (top-right default). Instance configuration. |
| `--polar-toast-position` | polar-toast-host | YES (`position`) | Where toasts stack. Instance configuration. |
| `--polar-toast-duration` | polar-toast-host | YES (`toast-duration`) | Default auto-dismiss duration (5000ms default); individual toasts override per-dispatcher payload. |

**Plus one new token introduced during the typography walkthrough: `--polar-size-scale`** (CSS-variable-only). Maps the semantic size values used by the `size="..."` attribute pattern (below) to numeric multipliers. Default: `small=0.85, default=1.0, large=1.15, hero=1.35`. Tenant override changes "what hero feels like" once and every hero-sized element scales accordingly.

**Tally: 8 tokens with attribute alias, 23 CSS-variable-only (22 originally + the new size-scale token).**

#### Design pattern commitments

Four interlocking patterns ship alongside the token catalog. The WebComponent Styling Guide narrative documents each with worked examples per WC.

1. **`size="default|small|large|hero"` attribute pattern.** WCs that visually benefit from per-element scaling expose a `size` attribute with semantic values that scale font + spacing + padding + interactive-target-sizes proportionally together (not just font). Per-WC the available size set varies; not every WC has all four. Resolves a common design need ("this CTA should be larger") via semantic markup rather than per-element font-size-base overrides (which would scale only the font, breaking spacing + interactive-target-sizes). Mapping from semantic size → multiplier is the `--polar-size-scale` token above; tenants override the scale once tenant-wide.

2. **State attribute pattern: `[selected]`, `[disabled]`, `[active]`, `[loading]`, `[hovered]`, `[featured]`.** WCs expose their state via standard HTML attributes; designers style state variations via CSS attribute selectors. The plan-picker selected/deselected visualization is the canonical example: `polar-plan-card[selected]` colors the surface + applies the brand primary; `polar-plan-card:not([selected])::after` overlays a 10%-darken pseudo-element. Dynamic state changes (customer clicks a different plan) propagate via attribute mutation → CSS re-evaluates automatically; zero JavaScript glue in the host page. Industry-standard pattern (Shoelace / Adobe Spectrum / IBM Carbon / Material Web Components all work this way).

3. **Lock-token enforcement (brand-consistency policy).** Per-token toggle in the tenant admin theme editor (covered in the theme-editor sub-section). Locked tokens emit with `!important` in the SignalR-injected tenant theme stylesheet, defeating any inline attribute override on a WC instance. Designers see the failure visibly in browser devtools so they're not silently confused. Lock-token requires the value to currently pass WCAG contrast checks (can't lock an inaccessible color); this is the accessibility-first editor (covered later) enforcing the floor.

4. **Tenant-AI policy applied to AI-dependent WCs.** AI-driven WCs (review summarization, future NL search, future personalized recommendations, gift-finder wizard) require tenant-supplied validated AI provider credentials (no SaaS-master fallback). Each AI WC checks `tenant.HasValidatedAiCredentials` before rendering: if true, generates / displays the AI output normally; if false, renders nothing for end-customers and a tenant-admin-only placeholder ("Configure AI in tenant settings to enable") with deep-link to the AI settings page. Same policy applied retroactively to v1.3 translation (drops its previous SaaS-master fallback tier). One consistent mental model across all AI features; one set of validation infrastructure.

#### WC catalog + agent-readable frontmatter spec

**85 Web Components scoped across v1.4.0 + v1.4.x + v1.5+** (18 baseline already in original PLAN.md + 67 additions designed during the 2026-05-19 walkthrough across 24 question batches). Per-WC ship-target distribution:

- **v1.4.0 launch:** ~69 WCs (18 baseline + 28 storefront + 12 reporting + 11 merchandising)
- **v1.4.x patches:** ~7 WCs (cross-sell, popups, pre-order, waitlist, charity-donation, coupon popup, exit-intent)
- **v1.5+ later:** ~7 WCs (impact-statement, build-your-own-bundle, gift-finder-wizard, tip-jar, influencer-storefront, live-shopping-event, polar-product-360-viewer 3D-mode variant)
- **Deferred to dedicated planning:** 2 WCs (polar-product-comparison-table + polar-plan-comparison-table — task #78)

Plus the 2-FA / passkey / SSO / reviews / media-gallery / AI-summary additions all introduced during the walkthrough and ship in v1.4.0.

**Agent-readable frontmatter spec.** Every WC's documentation (both DocFX article + per-WC `README.md` next to its source) carries YAML frontmatter so the agent-driven marketplace constructor can mechanically filter the catalog when generating a specific marketplace.

```yaml
---
wc: polar-product-card                          # WC tag name
purpose: Display a single product with image, name, price, optional badges
data-source: IStorefrontCatalogProvider.GetProductAsync
backend-deps: []                                # new-features-needed; empty = none
composes-with:
  - polar-product-grid                          # commonly-paired-with
  - polar-product-detail
  - polar-add-to-cart-button
  - polar-stock-indicator
  - polar-product-rating
conflicts-with: []                              # WCs that shouldn't coexist
required-tenant-config:
  - catalog must have at least one published product
audience: anonymous-ok                          # anonymous-ok | authenticated-required
deployment: embed-anywhere                      # embed-anywhere | tenant-storefront-only
ship-target: v1.4.0                             # v1.4.0 | v1.4.x | v1.5+
decision-tree-tags:                             # for agent niche-filtering
  - physical-goods
  - digital-goods
  - subscriptions
  - b2c
  - b2b
emits-events:
  - polarAddToCartTriggered
listens-to-events:
  - polarVariantChanged
accessibility:
  wcag-level: AA
  color-non-conveyance: stock-badge always paired with text label
  keyboard-reachable: yes
  screen-reader-tested: yes
---
```

The agent reads this YAML, evaluates the marketplace's niche profile (e.g. "selling digital downloads, B2C, low-volume"), and selects WCs whose `decision-tree-tags` + `audience` + `deployment` + `required-tenant-config` match. The frontmatter is the contract; the rich HTML/Markdown documentation below it is for human readers.

##### Baseline WCs (18 — already in original v1.4.0 catalog with revisions)

Dropped from original: `polar-login-button` + `polar-logout-button` (confusing on partner sites; account-menu is the unified replacement).

| WC | Purpose | Deployment | Ship target |
|---|---|---|---|
| `polar-product-card` | Single product display | embed-anywhere | v1.4.0 |
| `polar-product-grid` | Grid of product cards | embed-anywhere | v1.4.0 |
| `polar-product-detail` | Product detail page | embed-anywhere | v1.4.0 |
| `polar-mini-cart` | Mini cart dock icon + popover | embed-anywhere | v1.4.0 |
| `polar-cart-drawer` | Slide-in cart drawer | embed-anywhere | v1.4.0 |
| `polar-checkout-button` | Buy-now CTA (skips cart) | embed-anywhere | v1.4.0 |
| `polar-checkout-page` | Inline auth + multi-step checkout flow | embed-anywhere | v1.4.0 |
| `polar-account-menu` | Unified sign-in / account widget (replaces login/logout pair) | tenant-storefront-only | v1.4.0 |
| `polar-order-history-list` | Customer's past orders | tenant-storefront-only | v1.4.0 |
| `polar-wallet-balance` | Customer's wallet balance display | tenant-storefront-only | v1.4.0 |
| `polar-wallet-topup-flow` | Add funds to wallet | tenant-storefront-only | v1.4.0 |
| `polar-storefront-script` | Razor TagHelper bootstrap (MVC convenience) | embed-anywhere | v1.4.0 |
| `polar-toast-host` | Toast notification renderer (SignalR-subscribed) | embed-anywhere | v1.4.0 |
| `polar-product-search` | Search input + autosuggest dropdown | embed-anywhere | v1.4.0 |
| `polar-product-filters` | Faceted filter sidebar | embed-anywhere | v1.4.0 |
| `polar-saved-addresses` | Customer's saved billing/shipping addresses | tenant-storefront-only | v1.4.0 |
| `polar-saved-payment-methods` | Customer's saved cards | tenant-storefront-only | v1.4.0 |
| `polar-subscription-list` | Customer's active subscriptions | tenant-storefront-only | v1.4.0 |

##### Storefront additions (30 from walkthrough — including 2FA, passkey, media gallery, reviews, AI summary, SSO)

| WC | Purpose | Deployment | Ship target | Notes |
|---|---|---|---|---|
| `polar-category-tile` | Visual tile for one category | embed-anywhere | v1.4.0 | |
| `polar-category-grid` | Grid of category tiles | embed-anywhere | v1.4.0 | |
| `polar-breadcrumb-trail` | Hierarchical breadcrumb navigation | embed-anywhere | v1.4.0 | SEO essential |
| `polar-department-menu` | Mega-menu of departments + categories | embed-anywhere | v1.4.0 | |
| `polar-variant-selector` | Color/size/material picker; emits `polarVariantChanged` event | embed-anywhere | v1.4.0 | Honors PolarSharp's parent + variants → N Polar Products arch |
| `polar-quantity-stepper` | +/- quantity input | embed-anywhere | v1.4.0 | |
| `polar-add-to-cart-button` | Standalone add-to-cart (vs buy-now) | embed-anywhere | v1.4.0 | |
| `polar-stock-indicator` | Inventory badge (in-stock / low / out-of-stock) | embed-anywhere | v1.4.0 | |
| `polar-cart-summary` | Full `/cart` page line-items list | embed-anywhere | v1.4.0 | |
| `polar-promo-code-input` | Discount code entry | embed-anywhere | v1.4.0 | |
| `polar-shipping-estimator` | ZIP-based shipping rate preview | embed-anywhere | v1.4.0 | Cart-abandonment reducer |
| `polar-address-form` | Billing/shipping input + autocomplete | embed-anywhere | v1.4.0 | |
| `polar-payment-method-picker` | CC / wallet / saved card selection | embed-anywhere | v1.4.0 | |
| `polar-plan-picker` | 2-3 card pricing picker with selection state | embed-anywhere | v1.4.0 | Reference example for state attribute pattern |
| `polar-product-media-gallery` | Amazon-style image+video gallery | embed-anywhere | v1.4.0 | NEW from Q1; needs PolarMediaFileBase additions |
| `polar-2fa-setup` | TOTP + SMS 2FA enrollment UI | tenant-storefront-only | v1.4.0 | NEW from Q2 |
| `polar-2fa-challenge` | 2FA challenge during sign-in | embed-anywhere | v1.4.0 | NEW from Q2 |
| `polar-passkey-setup` | Passkey/WebAuthn enrollment UI | tenant-storefront-only | v1.4.0 | Uses .NET 10 native passkey APIs |
| `polar-passkey-challenge` | Passkey challenge during sign-in | embed-anywhere | v1.4.0 | Uses .NET 10 native passkey APIs |
| `polar-search-results` | Full-page search results display | embed-anywhere | v1.4.0 | |
| `polar-recently-viewed` | "Recently viewed" carousel (localStorage + cross-device sync) | embed-anywhere | v1.4.0 | |
| `polar-shoppable-image` | Editorial image with clickable product hotspots | embed-anywhere | v1.4.0 | Differentiator from Shopify |
| `polar-product-360-viewer` | Interactive 360° product photography | embed-anywhere | v1.4.0 (spin-frames mode); v1.5+ (3D model mode) | Split ship target |
| `polar-size-guide` | Interactive size guide for apparel | embed-anywhere | v1.4.0 | May be superseded by universal sizing engine (task #88) |
| `polar-product-questions` | Customer Q&A section | embed-anywhere reads / tenant-storefront-only writes | v1.4.0 | |
| `polar-product-rating` | Average star rating + distribution chart | embed-anywhere | v1.4.0 | |
| `polar-product-reviews` | Full reviews list with filtering + helpful-voting | embed-anywhere reads / tenant-storefront-only writes | v1.4.0 | |
| `polar-product-review-form` | Write-a-review (verified purchasers only) | tenant-storefront-only | v1.4.0 | |
| `polar-product-ai-summary` | AI-generated "What customers love / criticize" summary | embed-anywhere | v1.4.0 | Major differentiator from Shopify; tenant-AI policy applies |
| `polar-sso-button-group` | Renders configured SSO providers' sign-in buttons | embed-anywhere | v1.4.0 | Top 4 providers v1.4.0; others v1.4.x |

##### Reporting additions (13)

| WC | Purpose | Deployment | Ship target |
|---|---|---|---|
| `polar-order-detail` | Single-order drilldown | tenant-storefront-only | v1.4.0 |
| `polar-order-tracking` | Shipment tracking display | tenant-storefront-only | v1.4.0 |
| `polar-invoice-viewer` | Invoice PDF download + print view | tenant-storefront-only | v1.4.0 |
| `polar-receipt` | Post-purchase confirmation receipt | embed-anywhere | v1.4.0 |
| `polar-spend-summary` | "You've spent $X this year" personalized stats | tenant-storefront-only | v1.4.0 |
| `polar-subscription-detail` | Single-subscription drilldown (change/cancel) | tenant-storefront-only | v1.4.0 |
| `polar-refund-status-tracker` | Refund-request timeline | tenant-storefront-only | v1.4.0 |
| `polar-benefit-list` | Active customer benefits (license keys, downloads, Discord roles) | tenant-storefront-only | v1.4.0 |
| `polar-license-key-display` | Single license-key with copy + activation status | tenant-storefront-only | v1.4.0 |
| `polar-download-list` | Customer's downloadable files | tenant-storefront-only | v1.4.0 |
| `polar-loyalty-status` | Tier + points + progress | tenant-storefront-only | v1.4.0 | NEW loyalty system (task #85) |
| `polar-referral-tracker` | Referrals + rewards earned | tenant-storefront-only | v1.4.0 | NEW referral system (task #86) |
| `polar-impact-statement` | Sustainability messaging ("planted 3 trees") | embed-anywhere | v1.5+ |

##### Merchandising additions (24)

| WC | Purpose | Deployment | Ship target |
|---|---|---|---|
| `polar-promo-banner` | Site-wide promo bar | embed-anywhere | v1.4.0 |
| `polar-countdown-timer` | Sale-ending countdown ticker | embed-anywhere | v1.4.0 |
| `polar-flash-sale-card` | Time-limited product card with countdown | embed-anywhere | v1.4.0 |
| `polar-trust-badges` | Payment + security + guarantee badges | embed-anywhere | v1.4.0 |
| `polar-featured-collection` | "Featured items for {category}" carousel | embed-anywhere | v1.4.0 |
| `polar-curated-collection` | Manually-curated product list | embed-anywhere | v1.4.0 |
| `polar-occasion-shop` | Themed seasonal collections | embed-anywhere | v1.4.0 |
| `polar-up-sell-card` | "Upgrade to Premium for $X more" inline upsell | embed-anywhere | v1.4.0 |
| `polar-buy-x-get-y` | "Buy 2 get 1 free" promo display | embed-anywhere | v1.4.0 |
| `polar-bundle-offer` | "Save 20% on this bundle" display | embed-anywhere | v1.4.0 |
| `polar-discount-tier-badge` | Cart-aware tier progress ("$50 more for 10% off") | embed-anywhere | v1.4.0 |
| `polar-email-capture` | Newsletter signup with discount incentive | embed-anywhere | v1.4.0 |
| `polar-recently-purchased-by-others` | Social-proof ticker | embed-anywhere | v1.4.0 |
| `polar-cross-sell-grid` | "Customers also bought" grid | embed-anywhere | v1.4.x |
| `polar-coupon-popup` | Triggered modal with discount code | embed-anywhere | v1.4.x |
| `polar-exit-intent-modal` | Modal on cursor-leaves-viewport | embed-anywhere | v1.4.x |
| `polar-pre-order-card` | Pre-order product card with launch countdown | embed-anywhere | v1.4.x |
| `polar-waitlist-button` | "Notify me when back in stock" signup | embed-anywhere | v1.4.x |
| `polar-charity-donation-add-on` | Cart-level charity donation | embed-anywhere | v1.4.x |
| `polar-build-your-own-bundle` | Interactive bundle builder | embed-anywhere | v1.5+ |
| `polar-gift-finder-wizard` | AI-driven conversational gift recommender | embed-anywhere | v1.5+ |
| `polar-tip-jar` | Tip the maker/creator | embed-anywhere | v1.5+ |
| `polar-influencer-storefront` | Curated micro-storefront by affiliate | embed-anywhere + tenant-storefront-only (dashboard) | v1.5+ |
| `polar-live-shopping-event` | Streaming live-shopping event with chat | embed-anywhere | v1.5+ |

##### Deferred to dedicated planning session

| WC | Reason | Task |
|---|---|---|
| `polar-product-comparison-table` | Side-by-side comparison is vastly complex (specs schema + selection persistence + comparison overlay UX + accessibility). To be addressed after core store functionality is fully tested + implemented. | #78 |
| `polar-plan-comparison-table` | Same — feature matrices for subscription plans. Co-deferred. | #78 |

#### Auth-wizard + SSO architecture

**Anonymous-first deployment model.** Site visitors to partner / third-party sites browse + add to cart anonymously (guest cart persists via signed cookie). Authentication happens inline as a step inside `polar-checkout-page` — only when the customer actually proceeds to checkout. No standalone login/logout WCs in the catalog for embed-anywhere contexts (`polar-login-button` + `polar-logout-button` dropped from the original v1.4.0 plan because of partner-site UX confusion where the partner has its own auth + a tenant-store auth button alongside would feel like a phishing trap). `polar-account-menu` is the unified replacement for the tenant's own storefront (renders "Sign in" or "Welcome, [name]" depending on auth state in a tenant-storefront-only context).

##### Inline checkout-page auth flow (5 steps)

The host page never has to know about auth state — `polar-checkout-page` handles every path:

```
Customer hits "Checkout" from cart on partner or tenant-storefront site
  ↓
polar-checkout-page renders
  ↓
Step 1 — Email + auth-method selection
  Server lookup: did this email previously buy from this tenant?
    Yes  → "Welcome back, [name]! Sign in to use saved details + wallet
            OR continue as guest"
            (sign-in invokes password OR polar-2fa-challenge OR
             polar-passkey-challenge OR polar-sso-button-group per the
             customer's enabled methods)
    No   → "Continue as guest" OR "Create an account to track orders +
            earn rewards" (account creation continues inline; no separate
            signup wall)
  ↓
Step 2 — Shipping address (pre-filled if signed in; polar-saved-addresses)
  ↓
Step 3 — Payment method (polar-payment-method-picker)
  If signed in AND wallet has balance → wallet visible as payment option
  If guest OR no wallet balance → card-only
  ↓
Step 4 — Review + place order
  ↓
Step 5 (only for new-account-during-checkout path)
  Set password (optional; verification email/SMS sent per per-tenant
  verification-timing config)
  Optional 2FA / passkey enrollment prompt per per-tenant
  2fa-passkey-timing config
```

The order processes through this flow regardless of whether the customer signed in via password, SSO, 2FA-challenge, passkey, or continued as guest. Authentication state is captured at step 1; the flow continues identically through steps 2-4.

##### Per-tenant signup configuration

A new `TenantSignupConfig` entity (standard 5-layer tenant isolation) carries 7 per-tenant configurable knobs that let each marketplace tune signup for its niche / compliance profile / risk tolerance. The agent-driven marketplace constructor sets these per the tenant's niche profile when scaffolding a new marketplace. **8 of 8 auth-wizard decisions tilted to per-tenant configurability** (or platform-default + per-tenant override) during the walkthrough — reinforces that the agent-driven model wants per-marketplace tuning, not platform-wide rigid defaults.

| Knob | Type | Default | Options |
|---|---|---|---|
| Account identifier | enum | `email-only` | `email-only` / `email + optional-username` / `email + required-username` |
| Phone number at signup | enum | `optional` | `optional-always` / `required-only-when-customer-opts-into-SMS-features` / `required-at-signup` |
| Verification channel | enum | `email-only` | `email-only` / `email-or-SMS-customer-choice` / `email-and-SMS-both-required` |
| Verification timing | enum | `async-after-order` | `async-after-order` (order processes; verify later) / `blocking-before-order` (verify first; then order) |
| Password rules | object | platform default | Platform default = min 12 chars + complexity + HaveIBeenPwned breach check (security floor; tenant cannot disable). Tenant override may tighten (e.g. min 15 + forced rotation for regulated marketplaces) OR loosen above the platform-enforced minimum of 8 chars + breach check. |
| 2FA / passkey enrollment timing | enum | `prompted-on-first-sign-in` | `prompted-during-signup` / `prompted-on-first-sign-in` / `deferred-to-account-settings-only` / `required-must-enroll-at-signup` |
| Guest → account conversion | enum | `on-order-confirmation-page` | `on-order-confirmation-page` / `via-post-purchase-email` / `both` / `disabled` |
| Account recovery flow | enum | `email-link-only` | `email-link-only` / `email-or-SMS` / `email-link + backup-codes-at-signup` / `combinations` |

##### Per-tenant social SSO architecture

Per-provider packages mirror the v1.3 translation-provider + AI-provider patterns:

```
PolarSharp.MultiTenant.Identity.Sso.Google          v1.4.0
PolarSharp.MultiTenant.Identity.Sso.Microsoft       v1.4.0  (Microsoft Identity Platform — personal + work/school)
PolarSharp.MultiTenant.Identity.Sso.Facebook        v1.4.0
PolarSharp.MultiTenant.Identity.Sso.Apple           v1.4.0  (commonly required for iOS App Store compliance)
PolarSharp.MultiTenant.Identity.Sso.GitHub          v1.4.1  (popular for dev-tooling marketplaces)
PolarSharp.MultiTenant.Identity.Sso.LinkedIn        v1.4.1  (B2B marketplaces)
PolarSharp.MultiTenant.Identity.Sso.X               v1.4.2  (formerly Twitter)
PolarSharp.MultiTenant.Identity.Sso.Snapchat        v1.4.2  (Snap Login Kit; web SSO is newer + limited)
PolarSharp.MultiTenant.Identity.Sso.Pinterest       v1.4.2
PolarSharp.MultiTenant.Identity.Sso.TikTok          v1.4.2  (TikTok Login Kit)
```

YouTube uses Google SSO (same credentials, different scopes); Instagram uses Facebook SSO (Meta-owned). No separate packages for those. Each per-provider package wraps the corresponding `Microsoft.AspNetCore.Authentication.{Provider}` package (where one exists — Google, Microsoft, Facebook, Apple) OR implements custom OAuth/OIDC for niche providers (Snapchat, Pinterest, TikTok).

**Per-tenant BYOK + real-time validation (mirrors tenant-AI policy; task #87).** Tenant registers OAuth app per provider with the provider directly (the OAuth consent screen will show "Sign in to {tenant marketplace name} with Google" using the TENANT's app, not a SaaS-shared app — brand integrity). Tenant enters `client_id` + `client_secret` per provider in the tenant admin's dedicated SSO settings page. NEW entity `TenantSsoProvider` (per tenant + per provider; standard 5-layer tenant isolation; credentials encrypted at rest via `IPolarSecretProtector`).

On credential save: real-time validation against the provider's OIDC discovery endpoint / token-introspection. Daily IHostedService re-validation. Customer-facing SSO button only renders when credentials are validated. Tenant admin gets a notification via the v1.3 PrepaidWallets.Notifications dispatcher when credentials become invalid (provider revoked, billing lapsed, etc.).

##### Account-merging behavior

Default: **auto-link with confirmation prompt**. When a customer signs in with a new SSO provider whose email matches an existing account:

```
Customer clicks "Sign in with Google"
  ↓
OAuth flow returns email + Google account-id
  ↓
PolarSharp: does this email match an existing account in this tenant?
  Yes (matches an existing password account) →
    "We noticed you've used john@example.com here before with a password.
     Sign in with that to link your Google account to your existing
     account (recommended), OR continue with Google to create a separate
     account for this email."
  No (no existing account) →
    Create new account via Google identity.
```

Customer-driven; safe (verifies they own both); avoids silent account-takeover risk. Same logic across password + 2FA + passkey + every SSO provider.

##### 2FA + passkey integration

Backend: ASP.NET Core Identity native APIs surfaced through `PolarSharp.MultiTenant.Identity`. 2FA support exists in ASP.NET Core Identity since .NET Core 2; passkey/WebAuthn ships natively in ASP.NET Core 10 (verified via Microsoft Learn 2026-05-19; see memory note `feedback_verify_dotnet_capabilities.md`). PolarSharp wires both into the account flow (~1-2 days each of focused integration work — uses existing framework primitives; no Fido2.NetFramework dependency needed).

4 new WCs ship in v1.4.0:
- `polar-2fa-setup` (account-section UI: QR code for TOTP, recovery codes, SMS phone number) — tenant-storefront-only
- `polar-2fa-challenge` (auth-flow modal for the 6-digit code) — embed-anywhere (used by `polar-checkout-page`)
- `polar-passkey-setup` (account-section UI: register/manage passkeys with platform authenticator: Touch ID / Face ID / Windows Hello / hardware key) — tenant-storefront-only
- `polar-passkey-challenge` (auth-flow modal for the WebAuthn assertion) — embed-anywhere

SMS sending reuses the v1.3 PrepaidWallets.Notifications Twilio channel (no new SMS plumbing). Tenant configures their Twilio credentials once; both wallet notifications + SMS-2FA share the channel.

##### Tenant-admin SSO settings page

Dedicated `/admin/sso` route in the tenant admin Razor RCL. Lists supported providers (one row per provider, ordered by popularity); each row shows: provider icon + name + enable toggle + credentials input fields (client_id + client_secret) + validation status indicator (green check / red X / amber "needs reconfigure") + last-validated timestamp + "test now" button. Same UX pattern as the existing v1.3 AI provider config + v1.2 translation config.

##### Distinction from `PolarSharp.MultiTenant.Identity.KeyCloak` (v1.2.x existing)

KeyCloak (existing) is for **enterprise SSO** — the SaaS host's own staff signs into their internal admin via KeyCloak (realm-based; realm roles map to PolarSharp roles via `KeyCloakClaimsTransformer`). Customer-facing social SSO (v1.4.0 new work) is for **the tenant's customers** signing into the tenant's marketplace. Different audiences; different infrastructure; both coexist in a single deployment without conflict.

#### Theme editor (accessibility-first design)

**Reframed during the 2026-05-19 walkthrough from "additive accessibility" to "accessibility-first".** The editor's primary job is **"help the designer ship an accessible theme by default"** — accessibility isn't a feature to add, it's the editor's spine. Three reasons for the reframe (locked in task #74 + #89): (1) agent-driven marketplace construction needs sane defaults — AI agents won't reliably make accessibility decisions correctly; (2) retrofitting accessibility is way harder than building it in (Stripe / Adobe Spectrum / IBM Carbon bake it into their design system at the token level; Shopify / Webflow / Wix treat it as opt-in); (3) "different + better than Shopify" is a tangible product differentiator agents can advertise to merchants.

##### Editor canvas layout

```
┌─ Theme Editor — Tenant: {tenant-name} ────────────────────────┐
│                                                                │
│  ┌── Tokens (left panel) ──┐  ┌── Live Preview (right) ──┐    │
│  │ Colors (12)             │  │ [ Normal | Deuteranopia  │    │
│  │   --polar-primary       │  │   Protanopia | Tritanopia│    │
│  │     [color picker]      │  │   high-contrast prefs ]  │    │
│  │     #0066cc  🔒         │  │                          │    │
│  │     ✓ AA on surface     │  │  (iframe loads tenant's  │    │
│  │     ⚠ Fails AA on text  │  │   storefront with draft  │    │
│  │     Suggest: #003d80    │  │   theme injected via     │    │
│  │                         │  │   ?themeDraft= signed    │    │
│  │   --polar-accent ...    │  │   token; cycles homepage │    │
│  │ Typography (6)          │  │   / product / cart /     │    │
│  │ Spacing (3)             │  │   checkout sample pages) │    │
│  │ Shape (3)               │  │                          │    │
│  │ Motion (2)              │  │                          │    │
│  │ Component-specific (4)  │  │                          │    │
│  └─────────────────────────┘  └──────────────────────────┘    │
│                                                                │
│  ┌── Accessibility Score ──────────────────────────────────┐   │
│  │ WCAG AA: 27 of 31 tokens pass    ⚠                      │   │
│  │ Color-blind safe: 25 of 31 tokens pass  ⚠               │   │
│  │ [View 6 issues] [Publish anyway → requires sign-off]    │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                │
│  [Save Draft]  [Preview Link (TTL 7d)]  [Publish]  [Version]   │
└────────────────────────────────────────────────────────────────┘
```

Key behavioral elements (all locked):

| Element | Behavior |
|---|---|
| **Live preview iframe** | Loads the tenant's actual storefront with the draft theme injected via signed-token query param (`?themeDraft={signed-token}` with 7-day TTL). Cycles through sample pages (homepage / product-detail / cart / checkout) so designer sees the change in different contexts. |
| **Color-blindness simulator** | Tabbed switch on the iframe: `Normal` / `Deuteranopia` / `Protanopia` / `Tritanopia` / `high-contrast prefs`. Each tab applies a CSS color-transformation filter (lightweight: ~20-line JS color matrix per type) to the iframe contents. Designer toggles tabs to see how their theme renders for ~8% of customers with color-vision deficiencies. |
| **Per-token inline WCAG status** | Each color token shows an inline status indicator next to its picker: `✓ AA on surface` / `⚠ Fails AA on text` / etc. Updates continuously as designer changes the value. Color tokens are checked against multiple target pairings (surface, text, text-inverse, border, etc.) — fails on ANY relevant pairing surface the warning. |
| **Inline accessible-palette suggestions** | When a token fails WCAG, the editor surfaces a "Suggest: #003d80" inline alternative button. Designer clicks → token value updates to the suggestion (which passes WCAG against the relevant pairings while remaining visually close to the designer's intent — color-distance algorithm). |
| **Always-visible accessibility score panel** | Bottom of the editor; aggregated status across all 31 tokens (WCAG pass count + color-blind-safe pass count). Click "View 6 issues" to drill in. |
| **Publish-gate** | Critical accessibility failures (e.g. body text contrast fails) block publish. Tenant admin can override via typed acknowledgment ("I understand this theme has accessibility issues; publish anyway") — sign-off is audit-logged. Non-critical warnings (e.g. one color is hard to distinguish from another in protanopia) don't block but surface in the score. |
| **Lock-token requires WCAG pass** | Designer can lock a color token only if its value currently passes WCAG against all relevant pairings. Locking an inaccessible color is forbidden by design (the brand-consistency lock and the accessibility floor reinforce each other). |

##### Token-input controls (type-aware)

Each token type gets its own purpose-built control:

| Token type | Control |
|---|---|
| Color (`--polar-primary` etc.) | Color picker + eyedropper + recent-colors swatch + hex/rgb input + inline WCAG status indicator + inline accessible-palette suggestion when failing |
| Numeric (font-size-base, spacing-unit, border-radius, etc.) | Slider + raw number input |
| Enum (shadow-depth `none|sm|md|lg|xl`, animation-easing standard ease/ease-out/spring, toast-position) | Dropdown with the canonical enum values |
| Font-family | Dropdown of web-safe stacks + tenant's loaded webfonts (uploaded or linked to Google Fonts) |
| Position (mini-cart-position, toast-position) | Visual grid picker (3x3 grid: top-left / top-center / top-right / etc.) |
| URL (logo, hero image) | URL picker + drag-drop upload (drag-drop deferred to PolarSharp.MediaAndFileStorage task #82) |

##### Save + publish flow

**Draft + Publish with accessibility gate.** Designer's changes accumulate in a per-tenant draft (auto-saved per token change). Changes don't affect live storefront until designer clicks "Publish theme". Publish runs the accessibility validator across all 31 tokens; if any token has a CRITICAL failure (body-text contrast fails AA / locked-token resolves to inaccessible value / etc.), the publish blocks. Tenant admin override requires typed acknowledgment + is audit-logged. Non-critical warnings (color hard-to-distinguish in tritanopia, motion-duration is faster than user-prefers-reduced-motion implies) don't block — surface in the score, designer decides whether to fix.

**Preview-link sharing.** Designer clicks "Preview Link" → editor generates a signed URL (`storefront.example.com/?themeDraft={signed-token}`; TTL 7 days; one-time-use option). Designer sends to a stakeholder (or themselves on another device); stakeholder opens the storefront with the draft theme applied. Stakeholder cannot edit; can browse the live site with the draft styling to evaluate.

##### Version history + rollback

**Last 20 published theme versions retained per tenant** (each version is ~5-30KB of JSON; storage cost is trivial). Designer / tenant admin sees a "Theme History" panel listing versions with: timestamp + designer-who-published + accessibility-score + diff-from-prior-version (token-by-token list of changed values). One-click rollback creates a new version that's a copy of the target — preserves the audit trail (history is append-only, never overwrites). Tenants needing longer audit retention can export the JSON via the bulk export below.

##### Bulk JSON import/export

**Yes — import + export with validation.** Designer can export the current published theme OR active draft as a JSON file. Import a JSON file → replaces the current draft (validation runs: every token value validated by type + accessibility checked; warnings surfaced before commit). Use cases this enables: backup / restore, copy theme between staging+production tenants, share themes between sister marketplaces, hand-edit JSON for power users, AND critically — **programmatic theme deployment by AI agents** (the agent-driven marketplace constructor generates the theme JSON for a niche profile and POSTs it via the import API; theme applies once the agent confirms accessibility validation passed).

##### Tenant-admin integration + role separation

**Dedicated `/admin/theme` sub-page** inside the existing tenant admin Razor RCL (consistent admin shell; one login; one admin URL; matches v1.3 PrepaidWallets admin sub-page pattern).

**New `Designer` role for access control** — tenant admin can grant a contractor / agency / dedicated designer access to the theme editor without giving them full tenant admin access. Designer-role permissions scope is **per-tenant configurable** (locked decision): tenant admin picks (a) theme editing + reading catalog (minimum); (b) theme + content (catalog edit + manage media); (c) theme + content + announcements (also promo-banner + email-capture copy). Different marketplace org structures want different scopes.

##### Backend additions required (cross-referenced to tracking tasks)

| Addition | Effort | Tracked |
|---|---|---|
| `IAccessibilityValidator` service — validates token values against WCAG contrast ratios + color-blindness palette safety + accessible-palette suggestion algorithm | Medium (~1-2 weeks) | #89 |
| `TenantAccessibilityPolicy` field on `TenantBusinessProfile` — enforce-WCAG-AA-baseline (always on) + optional additional tenant requirements (WCAG AAA, EN 301 549 EU compliance) | Small | #89 |
| `CustomerAccessibilityPreferences` entity (per-customer; standard 5-layer isolation) — preferred-font, prefers-reduced-motion, prefers-high-contrast, color-blindness-type-if-known | Small | #89 |
| `TenantThemeDraft` entity (per-tenant; one draft per tenant) — accumulates token changes between publishes | Small | #74 |
| `TenantThemeVersion` entity (per-tenant; last 20 versions retained) — published theme history with audit trail | Small | #74 |
| `Designer` role definition + per-tenant configurable scope | Small (extends existing role infrastructure) | #74 |
| Theme editor JS dependencies — color-blindness simulator library (e.g. `color-blindness.js`), WCAG contrast checker (~20 lines), accessible-palette suggestion algorithm (medium complexity) | Medium | #74 + #89 |

Total estimated effort for the editor (above + UI implementation): ~4-6 weeks of focused work. Adds ~2-4 weeks vs the "additive accessibility" framing but ships a genuinely differentiated editor.

#### Distribution channels (NuGet + npm + CDN)

The Stencil-compiled WC bundle ships across **3 distribution channels in parallel**, each serving a different host scenario. Locked semver across all three — version 1.4.0 means **identical artifact + same bytes** on every channel.

| Channel | Package / URL | Host scenario |
|---|---|---|
| NuGet | `PolarSharp.EcommerceStorefronts.WebComponents` (static web assets via `wwwroot/`) | .NET tenants self-hosting alongside their existing ASP.NET Core app |
| npm | `@polarsharp/web-components` | Non-.NET tenants self-hosting via webpack / Vite / Rollup bundlers |
| CDN | `https://cdn.polarsharp.dev/wcs/{version}/...` (Cloudflare R2 + Workers) | Anyone hot-linking — partner sites, no-build static HTML, low-friction embeds, agent-generated marketplaces |

##### Bundle splitting strategy

Both shipped per channel:

- **Combined bundle** (`polarsharp-wcs.min.js`, ~150KB gzipped for all WCs). One script tag, all WCs available. Right for plain-HTML hosts and the agent-driven marketplaces (where the agent doesn't run a JS build pipeline).
- **Per-component ES modules** (`polarsharp-wc-product-card.min.js`, `polarsharp-wc-mini-cart.min.js`, etc.). Build-system hosts (webpack / Vite / Rollup) tree-shake to bundle only the WCs they actually use. Bundle sizes scale with usage.

Stencil's output config supports both shapes natively; the release pipeline produces both from the same source build.

##### CDN URL versioning pattern (both pinned + floating; pinned recommended)

Two URL patterns ship side-by-side:

```
Pinned (recommended in docs for production):
  https://cdn.polarsharp.dev/wcs/1.4.0/polarsharp-wcs.min.js
  → Immutable bytes for this URL; long browser cache (1 year max-age + immutable directive);
    safe to embed in long-lived host pages.

Floating (opt-in for development + experimentation):
  https://cdn.polarsharp.dev/wcs/1.4/polarsharp-wcs.min.js
  → Latest 1.4.x patch; short cache (5 min); auto-updates as PolarSharp ships patches.
  https://cdn.polarsharp.dev/wcs/latest/polarsharp-wcs.min.js
  → Latest stable across all majors; short cache; auto-updates even across majors.
```

Docs strongly recommend pinned for production; floating is opt-in for the cases where the tenant explicitly wants automatic patch updates. The agent-driven marketplace constructor uses pinned URLs by default + version-bumps via re-deployment cycles (predictable for the operator; no surprise CDN-side breakage).

##### CSP requirements (documented per channel; copy-paste examples)

Each channel's README + DocFX article ships a copy-paste `Content-Security-Policy` block. Concretely:

```http
# Required for the WC bundle to load + establish SignalR + render tenant theme:
Content-Security-Policy:
  script-src 'self' https://cdn.polarsharp.dev;                  # WC bundle
  connect-src https://signalr.polarsharp.dev;                    # SignalR hub
  style-src 'self' 'unsafe-inline';                              # tenant theme injection (CSS variables in :root)
  img-src 'self' https://{tenant-media-storage-origin};          # product images (tenant-specific)
  font-src 'self' https://{tenant-webfont-origin};               # webfonts if used
```

The `'unsafe-inline'` for style-src is required because the tenant theme is injected as an inline `<style>:root{...}</style>` block at page load. Tenants who run strict CSP without `'unsafe-inline'` can opt into a nonce-based variant (the WC bundle reads a `data-polar-style-nonce` attribute from the bootstrap element and applies it to injected styles) — documented as the strict-CSP recipe.

##### Subresource Integrity (SRI) hashes — auto-generated per release

Every released CDN artifact carries a SHA-384 SRI hash, published in release notes + the per-version DocFX article. Tenants pin via the standard `integrity` attribute:

```html
<script
  src="https://cdn.polarsharp.dev/wcs/1.4.0/polarsharp-wcs.min.js"
  integrity="sha384-{hash}"
  crossorigin="anonymous"></script>
```

Defends against CDN-side tampering OR CDN-account compromise. Standard pattern (Bootstrap, jQuery, Vue, etc. all publish SRI hashes). Build pipeline computes + writes the hash automatically; tenant just copies it from release notes.

##### CDN-unreachable fallback (built into the bundle's loader script)

PolarSharp ships a tiny loader script (`polarsharp-wcs-loader.min.js`, ~2KB) that hosts include FIRST. Loader tries the primary CDN; on load failure (404 / network unreachable / corporate firewall blocks), dynamically injects a configured fallback URL. Standard pattern (jQuery + Bootstrap docs both show this with a more bespoke fallback model). Concrete behavior:

```javascript
// Inside polarsharp-wcs-loader.min.js (conceptual):
async function loadBundle() {
  try {
    await loadScript('https://cdn.polarsharp.dev/wcs/1.4.0/polarsharp-wcs.min.js');
  } catch (cdnUnreachable) {
    // Try jsDelivr (npm-served same bytes):
    try {
      await loadScript('https://cdn.jsdelivr.net/npm/@polarsharp/web-components@1.4.0/dist/polarsharp-wcs.min.js');
    } catch (jsDelivrUnreachable) {
      // Try host's own self-hosted URL if they configured one:
      if (hostBootstrap.fallbackUrl) {
        await loadScript(hostBootstrap.fallbackUrl);
      } else {
        console.error('PolarSharp WC bundle unreachable from all sources; cart + checkout disabled.');
      }
    }
  }
}
```

Optional `data-polar-fallback-url` attribute on the bootstrap element lets the host specify their own self-hosted fallback (e.g. they have the NuGet package's static-web-assets bundle served from their own domain at `/polarsharp/wcs/1.4.0/polarsharp-wcs.min.js`).

##### SignalR + tenant theme bootstrapping (3 syntax options supported)

The bundle establishes a SignalR connection on init to receive the tenant theme + live config updates. **3 bootstrap syntaxes supported; bundle picks the first one it finds:**

```html
<!-- Option 1: bootstrap-element attribute (canonical) -->
<polar-storefront-script tenant-id="abc-123" embed-key="ek_live_..." />

<!-- Option 2: meta tag (more semantic for some hosts) -->
<meta name="polar-tenant" content="abc-123">
<meta name="polar-embed-key" content="ek_live_...">

<!-- Option 3: global JS init (most explicit) -->
<script>
  window.PolarSharp = { init: function() {} };
  window.PolarSharp.init({ tenantId: 'abc-123', embedKey: 'ek_live_...' });
</script>
```

Once any syntax is detected, the bundle:
1. Establishes a SignalR connection to `wss://signalr.polarsharp.dev/tenant/{id}` authenticated with the embed key
2. Receives the tenant theme as the first SignalR message (`InitialTheme` event with token JSON)
3. Injects a `<style>:root{...}</style>` block at page load (or with `data-polar-style-nonce` if the host uses nonce-based strict CSP)
4. Subscribes to `EmbedConfigUpdated` events for live theme updates (when the tenant publishes a new theme via the editor → the SignalR connection pushes the update; bundle re-injects the updated `:root` styles; live storefronts re-render within milliseconds)
5. Subscribes to `EmbedKeyRevoked` event for security (if tenant admin revokes the embed key, the bundle gracefully tears down + logs a console error so the tenant operator knows their embed credentials need rotation)

##### Release pipeline

- Build runs from `main` branch
- Stencil compiles to both combined bundle + per-component ES modules (single build invocation produces both shapes)
- SRI hashes computed for every published file
- Schema-snapshot CI gate (any WC API change requires the schema snapshot to be updated; Verify-based, mirrors the v1.3 GraphQL schema snapshot CI gate)
- 3 channels published in parallel from the same build artifacts
- Per-version DocFX article published with: SRI hashes, CSP recommendations, breaking changes (none expected within a major), perf notes, dependency-graph for the per-component ES modules

##### Release notes per-version DocFX article

Each release ships an article at `docs/articles/wc-release-notes/v{version}.md` covering: SRI hashes for every published file, CSP block updates if any new directives needed, new WCs added in this release, deprecated WCs (none in v1.4.0 — all WCs are new), perf changes (bundle size delta, render-perf delta), breaking changes (none within a major), accessibility-score baseline for the tenant theme this release ships (so designers know what's the new minimum).

#### New PolarSharp backend features + tracked TODOs

Phase 3 introduces a substantial set of NEW backend features alongside the WC catalog. Every new entity must satisfy the **5-layer tenant-isolation acceptance criteria** committed during the 2026-05-19 walkthrough (see memory note `feedback_tenant_isolation_every_new_entity.md`): (1) `ITenantOwned` interface + global query filter; (2) RLS policy in SqlServer + Postgres EF migrations; (3) SQLite per-tenant `.db` file placement; (4) MariaDB app-layer filter (no native RLS); (5) Cosmos `/tenantId` partition key. Plus single-tenant mode behavior (filter no-op; entity keeps `TenantId` column). Cross-tenant isolation regression tests required per the existing `CrossTenantIsolationTests` template.

##### Auth + Identity backend additions

| Addition | Effort | Ship target | Tracked |
|---|---|---|---|
| ASP.NET Core Identity 2FA wiring through `PolarSharp.MultiTenant.Identity` | ~1-2 days | v1.4.0 | #79 |
| Passkey wiring via .NET 10 native passkey APIs through `PolarSharp.MultiTenant.Identity` (no Fido2 library needed; verified via Microsoft Learn) | ~1-2 days | v1.4.0 | #80 |
| `PolarSharp.MultiTenant.Identity.Sso.{Google, Microsoft, Facebook, Apple}` per-provider packages | ~2-3 weeks total (~3-5 days per package) | v1.4.0 | #87 |
| `PolarSharp.MultiTenant.Identity.Sso.{GitHub, LinkedIn}` per-provider packages | ~1 week total | v1.4.1 | #87 |
| `PolarSharp.MultiTenant.Identity.Sso.{X, Snapchat, Pinterest, TikTok}` per-provider packages (some custom OAuth/OIDC for niche providers) | ~2-3 weeks total | v1.4.2 | #87 |
| `TenantSignupConfig` entity (8 per-tenant configurable signup knobs from auth-wizard design) | Small | v1.4.0 | #76 |
| `TenantSsoProvider` entity (per tenant + per provider; encrypted credentials via existing `IPolarSecretProtector`) | Small | v1.4.0 | #87 |
| `Designer` role + per-tenant configurable scope | Small (extends existing role infrastructure) | v1.4.0 | #74 |
| `HaveIBeenPwned` breach check integration in password rules | Small (~1 day; pure HTTP call against the breached-passwords k-anonymity API) | v1.4.0 | #76 |

##### Tenant-AI policy refactor

| Addition | Effort | Ship target | Tracked |
|---|---|---|---|
| Refactor v1.3 translation 3-tier → 2-state model (drop master/SaaS fallback; apply tenant-AI policy retroactively) | ~1 week | v1.4.0 | #83 |
| `TenantAiCredentialValidation` entity (per tenant + per provider) + `IAiCredentialValidator` service + daily IHostedService re-validation | ~2-3 days | v1.4.0 | #84 |
| Extend `IAiCompletionClient` with `TestAsync()` method; each of 5 v1.3 provider packages implements | ~1 day | v1.4.0 | #84 |

##### Reviews + AI summary backend

| Addition | Effort | Ship target | Tracked |
|---|---|---|---|
| `ProductReview` entity + moderation workflow + helpful-vote table | Medium (~1 week) | v1.4.0 | (part of chunk 3 catalog) |
| Review aggregates (cached avg rating + distribution per product, updated on review approval) | Small | v1.4.0 | (part of chunk 3 catalog) |
| `ProductAiSummary` entity + `AiSummaryGeneratorService` IHostedService (threshold-triggered regeneration: every N new reviews OR every M hours) | Medium (~1 week) | v1.4.0 | (part of chunk 3 catalog) |
| Photo upload for reviews (host-supplied URLs today; ergonomic upload via `PolarSharp.MediaAndFileStorage` in future) | Small (today); deferred to #82 for upload ergonomics | v1.4.0 (URLs) / future (upload) | #82 |

##### Q&A backend

| Addition | Effort | Ship target | Tracked |
|---|---|---|---|
| `ProductQuestion` + `ProductAnswer` entities + moderation queue + notification triggers (notify question-asker via v1.3 Notifications dispatcher when answered) | Medium (~1 week) | v1.4.0 | (part of chunk 3 catalog) |

##### Loyalty + Referral systems

| Addition | Effort | Ship target | Tracked |
|---|---|---|---|
| Loyalty system: `LoyaltyTier` + `LoyaltyPointsLedger` (event-sourced like wallet) + `LoyaltyRedemptionRule` + `LoyaltyEarningRule` entities; tier definitions; points earning/redemption mechanics; points expiry policies | Large (~2-3 weeks) | v1.4.0 | #85 |
| Referral system: `ReferralCode` (per customer) + `ReferralAttribution` + `ReferralReward` entities; attribution on signup (cookie + URL param tracking); reward triggering (post-refund-window); fraud prevention | Medium (~1-2 weeks) | v1.4.0 | #86 |

##### Catalog / merchandising backend additions

| Addition | Effort | Ship target | Notes |
|---|---|---|---|
| `ProductCategoryFeature` join table (per-category featuring; product_id + category_id + display_order + active dates) | Small | v1.4.0 | `polar-featured-collection` |
| `CuratedCollection` entity (name + description + cover-image + JSON product-ids array + active dates) | Small | v1.4.0 | `polar-curated-collection` + extends to `polar-occasion-shop` with date-range fields |
| `BuyXGetY` discount kind (new discount kind extending LocalDiscount; X products from set A trigger Y free products from set B) | Medium | v1.4.0 | `polar-buy-x-get-y` |
| `ProductBundle` entity (name + included product-ids + bundle price OR bundle discount percent + active dates) | Medium | v1.4.0 | `polar-bundle-offer` |
| `TieredDiscountRule` entity (thresholds + reward types: discount-pct / free-shipping / etc.) | Small | v1.4.0 | `polar-discount-tier-badge` |
| `EmailSubscription` entity + double-opt-in flow + integration with v1.3 PrepaidWallets.Notifications email channel | Small | v1.4.0 | `polar-email-capture` |
| `ShoppableImage` entity (image-url + JSON hotspots array `{x, y, productId, label?}`) + admin UI for hotspot positioning | Small | v1.4.0 | `polar-shoppable-image` |
| `SizeGuide` entity per-product or per-category (JSON size table + measurement units) | Small | v1.4.0 (may be superseded by universal sizing engine — see #88) | `polar-size-guide` |
| `PromoBanner` entity (content + active dates + targeting rules + dismiss behavior) | Small | v1.4.0 | `polar-promo-banner` |
| `PopupRule` entity (trigger type: time / scroll / exit-intent + frequency cap + content reference) | Small | v1.4.x | shared by `polar-coupon-popup` + `polar-exit-intent-modal` |
| `Preorder` status enum + `LaunchDate` column on `LocalProductVariant` + deferred-fulfillment handling in order pipeline | Small | v1.4.x | `polar-pre-order-card` |
| `ProductWaitlist` entity (product/variant + customer email + signed-up date + notified date) + stock-transition notification trigger | Small | v1.4.x | `polar-waitlist-button` |
| Cross-sell co-purchase aggregator IHostedService (top-N co-purchased SKUs per product, updated on every order; bounded store) | Medium | v1.4.x | `polar-cross-sell-grid` |
| `CharityCampaign` entity + new cart line-item type for charity donations + monthly payout aggregation IHostedService | Medium | v1.4.x | `polar-charity-donation-add-on` |

##### Media + media-storage

| Addition | Effort | Ship target | Tracked |
|---|---|---|---|
| `PolarMediaFileBase` additions: `DisplayOrder`, `AltText`, `IsPrimary`, `AssociatedVariantAxes`, `ThumbnailUrl`, `Polar360ViewerSpinUrl` (6 new columns on existing media table) | Small | v1.4.0 | #81 |
| Admin UI for managing media-file metadata (per-product media manager: drag-reorder, edit alt-text, mark primary, associate variant axes) | Small | v1.4.0 | #81 |
| `PolarSharp.MediaAndFileStorage` lift-shiftable package family (AWS S3, GCS, Azure Blob, Cloudflare R2, Dropbox, Box, OneDrive, GoogleDrive providers; per-tenant config; signed-URL handling; thumbnail generation; lifecycle policies) | Large; future planning session | future | #82 |

##### Accessibility infrastructure

| Addition | Effort | Ship target | Tracked |
|---|---|---|---|
| `IAccessibilityValidator` service (WCAG contrast ratio checker + color-blindness palette safety check + accessible-palette suggestion algorithm) | Medium (~1-2 weeks) | v1.4.0 | #89 |
| `TenantAccessibilityPolicy` field on `TenantBusinessProfile` (enforce-WCAG-AA-baseline always on; optional WCAG AAA or EN 301 549 EU compliance) | Small | v1.4.0 | #89 |
| `CustomerAccessibilityPreferences` entity (per-customer; preferred-font, prefers-reduced-motion, prefers-high-contrast, color-blindness-type-if-known) | Small | v1.4.0 | #89 |
| Per-WC axe-core regression tests + manual screen-reader testing checklist | Substantial; ongoing across the catalog (~4-6 weeks spread) | v1.4.0 baseline | #89 |
| `prefers-reduced-motion`, `prefers-contrast: high`, dyslexia-friendly opt-in font (OpenDyslexic) support across all WCs | Medium (~1 week aggregate) | v1.4.0 | #89 |

##### Theme editor backend

| Addition | Effort | Ship target | Tracked |
|---|---|---|---|
| `TenantThemeDraft` entity (per-tenant; one active draft; auto-saved on token changes) | Small | v1.4.0 | #74 |
| `TenantThemeVersion` entity (per-tenant; last 20 versions retained per tenant; audit trail for rollback) | Small | v1.4.0 | #74 |
| Signed-URL preview-link generator (`?themeDraft={signed-token}`; TTL 7 days) | Small | v1.4.0 | #74 |
| Theme editor JS dependencies: color-blindness simulator library (e.g. `color-blindness.js`) + WCAG contrast checker (~20 lines) + accessible-palette suggestion algorithm | Medium (~2-3 days for JS integration) | v1.4.0 | #74 + #89 |

##### Recently-viewed sync

| Addition | Effort | Ship target | Notes |
|---|---|---|---|
| `PolarApplicationUser.RecentlyViewedProductIds` column + sync endpoint (anonymous via localStorage; cross-device for authenticated) | Small | v1.4.0 | `polar-recently-viewed` |

##### Distribution-channel infrastructure

| Addition | Effort | Ship target | Tracked |
|---|---|---|---|
| Cloudflare R2 + Workers CDN setup at `cdn.polarsharp.dev` | Small (one-time infra setup) | v1.4.0 | #75 |
| SignalR hub at `signalr.polarsharp.dev` for tenant theme + live config-update push | Medium (existing SignalR infra exists from v1.4 plan; this extends it for theme + embed-key validation flow) | v1.4.0 | #75 |
| Release pipeline producing 3 channels in parallel + SRI hashes + schema-snapshot CI gate + per-version DocFX release-notes article | Medium (~1-2 weeks initial pipeline build) | v1.4.0 | #75 |
| Bundle loader script with CDN-unreachable fallback (~2KB) | Small | v1.4.0 | #75 |

##### v1.5+ deferred backend features

| Addition | Effort | Reason for deferral | Tracked |
|---|---|---|---|
| Build-your-own-bundle dynamic pricing rules engine | Medium-large | Substantially more complex than fixed `ProductBundle`; deserves own design pass | (part of chunk 3 catalog) |
| Gift-finder wizard AI integration (uses tenant's BYOK AI per the tenant-AI policy) | Medium; depends on tenant-AI policy refactor (#83) | Premium feature; v1.5+ alongside other AI-driven storefront features | (part of chunk 3 catalog) |
| Tip system (creator wallet routing + new `Tip` entity tracking individual tip transactions) | Medium; uses v1.3 wallet infrastructure | Requires creator-as-distinct-from-tenant model not in scope until later | (part of chunk 3 catalog) |
| Affiliate / influencer system (affiliate tier in Identity + commission rules + click-attribution + payout tracking + fraud detection) | Large (~3-4 weeks) | Major growth feature deserving own design pass | (part of chunk 3 catalog) |
| Live shopping streaming integration (Mux or Cloudflare Stream OR self-hosted SRS + real-time chat moderation + viewer analytics + product-surfacing timing) | Very large (~2-3 months) | Substantial infrastructure dependencies; deserves own multi-quarter design + implementation pass | (part of chunk 3 catalog) |
| Impact-statement metadata schema + 3rd-party partner integrations (Ecologi, One Tree Planted, MoreTrees) | Medium | Niche feature; tenants who need it can launch without and add later via custom HTML | (part of chunk 3 catalog) |
| `polar-product-360-viewer` 3D model mode (GLB / USDZ + WebXR AR on iOS) | Medium | Premium feature; spin-frames mode ships v1.4.0; 3D defers | (part of chunk 3 catalog) |

##### Tracked TODOs (deferred to dedicated planning sessions)

| Topic | Why deferred | Tracking task |
|---|---|---|
| **Side-by-side comparison capability** (polar-product-comparison-table + polar-plan-comparison-table) | Vastly complex (specs schema + selection persistence + comparison overlay UX + accessibility); to be addressed after core store functionality is fully tested + implemented | #78 |
| **Universal sizing engine integration** (user-supplied C# library) | User has developed a universal sizing engine in C#; integration likely follows lift-and-shift pattern (`PolarSharp.UniversalSizing.*` lift-safe core + `PolarSharp.UniversalSizing.Polar.*` bridges); supersedes basic `polar-size-guide` SizeGuide entity; design questions around how tenants tag products with universal sizes, the standardization vocabulary, cross-region conversion, etc. | #88 |
| **PolarSharp.MediaAndFileStorage** (lift-shiftable package family) | Covers all major cloud blob storage providers (AWS S3, GCS, Azure Blob, Cloudflare R2, Dropbox, Box, OneDrive, GoogleDrive); cross-cutting (used by media-gallery, download-list, license-key-display, invoice-viewer, receipt, future 360-viewer); design questions around abstraction shape, per-tenant config, signed-URL handling, thumbnail generation strategy, multipart upload, lifecycle policies, CDN integration | #82 |

##### Cross-cutting acceptance criteria (committed standards for every new entity + WC)

1. **5-layer tenant isolation** — ITenantOwned + RLS migration + per-tenant SQLite db + Cosmos partition + MariaDB app-layer filter + single-tenant mode. Memory note: `feedback_tenant_isolation_every_new_entity.md`.

2. **Machine-parseable agent-readable frontmatter** on every WC's documentation — YAML schema documented in chunk 3 above. Required for agent-driven marketplace construction. Memory note: `project_agent_driven_marketplaces.md`.

3. **WCAG 2.2 AA accessibility floor** on every WC + non-color-only state conveyance + theme editor publish-gate. Task #89.

4. **Tenant-AI policy enforcement** — every AI-dependent WC checks `tenant.HasValidatedAiCredentials` before rendering; no SaaS-master fallback. Task #83 + #84. Memory note (existing): the policy applies retroactively to v1.3 translation too.

5. **Per-WC documentation across all three surfaces** (per existing CLAUDE.md standing requirement) — inline XML comments (CS1591 build error) + per-package README.md + DocFX article. NEW for v1.4.0: every WC also gets coverage in the dedicated `web-component-styling-guide.md` Implementation Narrative (state attributes, CSS selector patterns, visual treatment recipes, accessibility patterns per WC).

6. **Cross-tenant isolation regression tests** per the existing `CrossTenantIsolationTests` template for every new entity that holds tenant-scoped data.

7. **Live Polar sandbox integration tests** for every new IPolarXxxApi wrapper (per memory note `feedback_live_polar_tests_required.md`) — pattern set by V20-002 / V20-003 / V20-004.

8. **AOT compatibility** — every package preserves the existing CI gate (`dotnet publish -p:PublishAot=true` → zero warnings). Public records use `required init` properties; no reflection; no `Activator.CreateInstance`.

9. **Lift-shiftable boundary on new package families** — any new lift-shiftable feature family (PolarSharp.MediaAndFileStorage when scoped; future universal-sizing-engine integration; v1.5+ affiliate/influencer system; etc.) MUST follow the established lift-and-shift pattern (`PolarSharp.{Family}.*` lift-safe core + `PolarSharp.{Family}.Polar.*` Polar-bridges) with CI guard verifying zero `PolarSharp.*` dependencies in the lift-safe core. Reference: existing PrepaidWallets pattern + EcommerceStorefronts pattern (both established this in v1.3.0).

---

---

## v2.0 launch strategy (added 2026-05-13 during v1.3.H pre-commit pass)

v2.0 is the **HTTP-completion + production-hardening release**. Three pillars:

### Pillar 1 — Complete the Polar HTTP wire (unblocks 7 deferred stubs)

Every TASK-V20-001 through TASK-V20-006 wires a deferred `PolarClient*Api` stub to live Polar request builders. These stubs ship in v1.3.0 as honest no-ops — they log a warning and return `UnexpectedFailure` — so the surrounding orchestration (refunds, license validation, business profile, inventory sync, catalog publishing, snapshot ingestion, onboarding) is testable today against host-supplied mocks but the actual Polar calls are pending. The Kiota request builders for each endpoint already exist in `src/PolarSharp/Generated/`; the work is composing them through the typed boundaries:

- TASK-V20-001 — `IPolarPublishingApi` (the biggest; covers product / benefit / discount / checkout-link create + update)
- TASK-V20-002 — `IPolarRefundsApi` (`POST /v1/refunds/`, `GET /v1/refunds/`)
- TASK-V20-003 — `IPolarLicenseKeysApi` (`POST /v1/license-keys/{id}/validate`)
- TASK-V20-004 — `IPolarOrganizationsApi` (`PATCH /v1/organizations/{id}`, `GET /v1/organizations/{id}`)
- TASK-V20-005 — `IPolarReportingApi` (`GET /v1/events/`, `/v1/orders/`, `/v1/subscriptions/`, `/v1/customers/`, `/v1/refunds/` — paginated)
- TASK-V20-006 — `KiotaPolarOnboardingApi` (programmatic + OAuth flows)
- TASK-V20-007 — `FakeDataSyncService` toggle branches (depends on TASK-V20-001)

### Pillar 2 — Production hardening (from PRODUCTION-READINESS-ANALYSIS.md)

The v1.3.H pre-commit audit (production-readiness analysis at `/Users/mollsandhersh/Repos/Polar.sh_Nuget/PRODUCTION-READINESS-ANALYSIS.md`) surfaced several P1 issues that v1.3.0 ships with **known and documented** but that v2.0 must address before broad production adoption:

- **TASK-V20-012** — RLS DDL actually wired into initial migrations (currently the layer-2 defense is documented but absent)
- **TASK-V20-013** — `AuditLogSaveChangesInterceptor` actually implemented ✅ **DONE 2026-05-19** — 225-line `SaveChangesInterceptor` shipped at `src/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore/AuditLogSaveChangesInterceptor.cs`, registered Scoped in `CatalogServicesExtensions`, attached via `AddInterceptors` in all 5 provider builders (SqlServer / Sqlite / PostgreSQL / MariaDb / CosmosDb). Stale claim corrected during the 2026-05-19 codebase stub audit.
- **TASK-V20-014** — Server-side query translation restored on SQL Server / PostgreSQL (`EfAdvancedReportingClient` uniformly materialises client-side today, only safe on SQLite at low row counts)
- **TASK-V20-015** — Walk the document's "Top 10 priorities" list and seed individual tasks (DLQ for webhook background queue, durable FakeDataSync channel, per-action transaction boundaries on publisher, etc.)

### Pillar 3 — Two new operator features (clone + wipe, designed during v1.3.H)

Mid-implementation in v1.3.H the project owner surfaced two operator features that didn't fit into v1.3.0's reporting+services scope but are essential for tenant lifecycle management. Designs in TASKS.md:

- **TASK-V20-010 — Tenant store clone / export-import.** Round-trips an entire tenant's catalog + business profile + translations as JSON. Lets a merchant fork their sandbox into production without manual re-entry, and lets PolarSharp deployments migrate between environments. Polar-side ids stripped at export time so the import produces a clean re-publish state.
- **TASK-V20-011 — Tenant store reset / wipe with safeguards.** Single-use confirmation token + typed-slug verification + explicit no-rollback acknowledgement + automatic export-before-wipe backup + `[RequireAppMasterAdmin]`-only invocation + dual audit-log writes (tenant + platform). Polar-side cleanup optional (`is_archived: true` since Polar has no DELETE).

The two features ship together so the workflow "export → wipe → re-onboard → import" is atomic from the operator's perspective.

### Branching + release strategy

- **v2-main branch** cut from the `v1.3.0` tag once v1.3.0 publishes
- Each TASK-V20-NNN merges to `v2-main` independently, behind the existing CI gate (`publish` job depends on `build-test` — no test failures can ship)
- **No breaking-change candidates** identified yet. The current plan's "Changed" sections are all additive; if a v2.0-only breaking change is required (e.g. un-sealing `WebhookXxxData` records to inherit from `PolarXxxBase` as the original v1.2.0 plan envisaged), the breaking change ships in a `v2.0.0` major bump with a migration guide; until then v2.0 is a minor bump from v1.3.x with the seven HTTP completions and four production-hardening items
- **Integration test infrastructure investment**: v2.0 needs a live Polar sandbox CI runner (gated by `POLAR_SANDBOX_TOKEN` env var, skipped on PRs from forks) that exercises every wired endpoint end-to-end. TASK-V20-009 already tracks this commission
- **Per-provider Testcontainers harnesses**: SQL Server + PostgreSQL Testcontainers for the RLS bypass tests (TASK-V20-012 deliverable) and the per-provider tenant isolation tests (audit-2 v2.0 deferral)
- **FakeDataSync concurrency harness**: requires seeded `TimeProvider` for background-service timing assertions; tracked alongside TASK-V20-007

### What v2.0 explicitly does NOT include

- Cross-tenant data analytics for the platform operator at the **customer level** — that violates the SaaS-tenant trust model. The four operator reports stay at the tenant level (aggregated counts and grades)
- Stripe API calls — PolarSharp does NOT talk to Stripe. Ever. Anywhere. v2.0 polls Polar's read-only Stripe fields (`account_id`, `payout_account_id`) and produces dashboard deep-links; nothing else
- Schema-breaking changes to v1.x consumer code without a migration guide and a deprecation period

### Estimated v2.0 cadence

- HTTP completion (Pillar 1): 6 sub-phases, one per stub. Each takes a focused 2–4 day cycle against the Polar sandbox
- Production hardening (Pillar 2): 4 named tasks plus the umbrella TASK-V20-015 catch-up. Roughly a 2-week sprint
- Clone + wipe (Pillar 3): coupled, ship together. Approximately a 1-week sprint after Pillar 1's onboarding + publisher tasks land (the clone export needs a working publisher to round-trip)

Total v2.0 cycle: target 6–8 weeks from `v1.3.0` tag to `v2.0.0` tag, with intermediate `v2.0.0-preview-N` releases as each pillar lands.
