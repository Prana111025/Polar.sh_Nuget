# TASKS.md

Authoritative task list for active and pending agentic work.

Use task IDs such as TASK-001. Include owner, status, dependencies, acceptance criteria, and verification steps.

---

## v2.0 — Honest deferrals from v1.2.0

The v1.2.0 release ships 25 new packages, 12 migration sets, and 441/441 passing tests. The following work was intentionally deferred — the abstractions, signatures, and DI registrations are in place, but the underlying Polar HTTP wiring is stubbed. Each task below restores live behavior or addresses a known limitation.

### TASK-V20-001 — Wire IPolarCatalogPublisher.PublishAsync to Polar HTTP

- **Status:** Deferred from v1.2.0 → planned for v2.0
- **Owner:** unassigned
- **Project:** Polar.sh_Nuget — PolarSharp.EcommerceStoreManagement
- **Context:** The publisher orchestrates local → Polar sync for products, benefits, discounts, and checkout links in dependency order. v1.2.0 ships the orchestration shell (preview/dry-run, plan computation, per-outcome persistence) but does NOT hit Polar's API; instead it logs "publish requested" and returns a Stub outcome.
- **What to do:** Implement the live HTTP path using `PolarClient.Organizations`, `PolarClient.OrganizationAccessTokens`, `PolarClient.Webhooks`, `PolarClient.Products`, `PolarClient.Benefits`, `PolarClient.Discounts`, `PolarClient.CheckoutLinks`. Honor idempotency via persisted `PolarProductId`/`PolarBenefitId`/etc. Wire variant + tier expansion, dependency order, partial-failure resume.
- **Acceptance:** Integration test (sandbox) seeds 10 products with variants and tiers, publishes successfully, re-publish is a no-op, simulated network drop mid-publish resumes from `OutOfSync` correctly.

### TASK-V20-002 — Wire IRefundService to Polar /v1/refunds ✅ DONE

- **Status:** ✅ Done — wired against `https://sandbox-api.polar.sh` and validated by live-sandbox integration tests.
- **Project:** PolarSharp.EcommerceStoreManagement.EntityFrameworkCore
- **What landed:** `PolarClientRefundsApi` calls `POST /v1/refunds/` and `GET /v1/refunds/?order_id=…` via the Kiota-generated `PolarClient`. Happy-path mapping (`RefundCreate` → `Refund`) is end-to-end. Error mapping uses HTTP status to discriminate: 404 → `OrderNotFound`, 400 → `AmountExceedsRefundable` / `AlreadyFullyRefunded` / `CurrencyMismatch` (via response body parsing), 5xx → `UnexpectedFailure`. Unknown-shape bodies fall back to `UnexpectedFailure` so callers always see a typed error.
- **Tests:** `tests/PolarSharp.EcommerceStoreManagement.Tests/PolarClientRefundsApiIntegrationTests.cs` carries `[Trait("Category","Integration")]` live-sandbox tests gated on `POLAR_SANDBOX_TOKEN`. Service-level logic (`RefundService`) covered by unit tests against a fake `IPolarRefundsApi`.
- **Closure note:** TASKS.md previously marked this "Deferred to v2.0" — that status was stale. The code shipped under V20-002 with sandbox validation. Status corrected during the 2026-05-20 audit pass.

### TASK-V20-003 — Wire ILicenseKeyValidator to Polar /v1/license-keys/{id}/validate ✅ DONE

- **Status:** ✅ Done — wired against `https://sandbox-api.polar.sh` and validated by live-sandbox integration tests.
- **Project:** PolarSharp.EcommerceStoreManagement.EntityFrameworkCore
- **What landed:** `PolarClientLicenseKeysApi` calls `POST /v1/license-keys/{id}/validate` via `PolarClient`. Response maps to `LicenseValidationResult` with `IsValid`, `CustomerId`, `ExpiresAt`, `ActivationsRemaining`, `InvalidReason`, `IsWithinGracePeriod`. Cache TTL (default 60s) + grace-period detection (per-tenant override or global 7 days) handled at the `LicenseKeyValidator` orchestrator layer.
- **Tests:** `tests/PolarSharp.EcommerceStoreManagement.Tests/PolarClientLicenseKeysApiIntegrationTests.cs` — live-sandbox tests gated on `POLAR_SANDBOX_TOKEN`. Validator-level caching + grace-period logic covered by unit tests against a fake.
- **Closure note:** Same as V20-002 — TASKS.md status was stale. Corrected 2026-05-20.

### TASK-V20-004 — Wire IPolarBusinessProfileService.SaveAsync to Polar Organizations PATCH ✅ DONE

- **Status:** ✅ Done — wired against `https://sandbox-api.polar.sh` and validated by live-sandbox integration tests.
- **Project:** PolarSharp.EcommerceStoreManagement.EntityFrameworkCore
- **What landed:** `PolarClientOrganizationsApi` PATCHes the writable subset (country, currency, tax behavior, OrganizationDetails) to `/v1/organizations/{id}`. Read-back updates the local mirror for `account_id` / `payout_account_id`. `RefreshPayoutStatusAsync` polls the live read-only fields; transitions `NotStarted → InProgress → Ready`. `BuildBankingSetupDeepLink` returns Polar's dashboard URL (no Stripe API call ever — per DECISIONS.md D-001).
- **Tests:** `tests/PolarSharp.EcommerceStoreManagement.Tests/PolarClientOrganizationsApiIntegrationTests.cs` — live-sandbox tests gated on `POLAR_SANDBOX_TOKEN`. Service-level FSM + field-set separation logic covered by unit tests against a fake.
- **Closure note:** Same as V20-002 — TASKS.md status was stale. Corrected 2026-05-20.

### TASK-V20-005 — Wire IReportSnapshotService to Polar resource endpoints

- **Status:** ✅ Done — closed 2026-05-14 at commit `358e242`
- **Owner:** unassigned
- **Project:** PolarSharp.Reporting
- **Context:** Snapshot tables (orders, line items, refunds, subscriptions, customers, benefit grants, events) are created via migrations; `RunSnapshotAsync` returns an empty report.
- **What landed:**
  - **Phase 1B–1H** — live wirings for the 7 new resources (products, customer-meters, license-keys, benefits, meters, checkout-links, discounts) with paired live-sandbox tests
  - **Phase 1.5** — live wirings for the original 5 resources (events, orders, subscriptions, customers, benefit-grants) with paired live-sandbox tests
  - **Phase 2** — `PerTenantSnapshotOrchestrator` + `IReportSnapshotTrigger` (per-tenant timer + heartbeat + idle-timeout + completion-event channel)
  - **Phase 3** — new `PolarSharp.Reporting.Identity` bridge package: `PolarSnapshotSignInManager` + `PolarSnapshotHeartbeatMiddleware` so Identity sign-in/sign-out drives the orchestrator automatically
  - **Phase 3 follow-up** — `PolarSnapshotTestApp` end-to-end demo app (separate from `PolarTestApp` to keep the AOT smoke test on the AOT-clean library code)
  - **Default `IPolarTenantScopeInitializer`** — closes the V20-005 Phase 2 design gap; two-phase API (`ResolveTenantAsync` + `SetCurrentTenant` extension) to work around AsyncLocal scoping
  - **Migration drift catch-up** — `ModelDriftCatchup` migrations across all 3 providers for the 7 new resource tables
  - **Drilldown E2E + 10k-customer perf gate** — `HierarchicalDrilldownEndToEndTests` (6 functional + 1 perf)
  - **Idempotency integration test** — `SnapshotIdempotencyIntegrationTests` (live sandbox; re-run yields zero new rows on every per-resource counter)
  - **SQLite `DateTimeOffset` ORDER BY fix** — provider-conditional value converter so the drilldown queries work on all 3 providers
- **Acceptance verified:** Idempotency test asserts re-run = no-op; perf test asserts 10k-customer top-level page < 100ms.
- **Deferred to v2.x (NOT blocking V20-005 closure):**
  - Order line items: Polar's `OrderItemSchema` doesn't expose `ProductId`; need `ProductPriceId → ProductId` lookup against the prices snapshot
  - Order refunds: Polar's Order has no nested refunds list; needs separate top-level `/v1/refunds/` ingestion pass with its own checkpoint
  - `BenefitGrant.BenefitName/Kind` enrichment: currently placeholder (`BenefitId` / `"unknown"`); needs join against benefits snapshot
  - `Event.PayloadJson`: Polar list endpoint omits the payload blob; needs per-event GETs (N+1) or a webhook-tap alternative

### TASK-V20-006 — Wire KiotaPolarOnboardingApi using v1.1.0 Kiota resource builders

- **Status:** Deferred → v2.0
- **Owner:** unassigned
- **Project:** PolarSharp.Onboarding
- **Context:** `IPolarOnboardingClient` programmatic + wizard flows are fully implemented through to the HTTP boundary; the bottom layer (`KiotaPolarOnboardingApi`) returns a stub `OnboardedTenantResult`.
- **What to do:** Implement using `PolarClient.Organizations` (POST), `PolarClient.OrganizationAccessTokens` (POST), `PolarClient.Webhooks.Endpoints` (POST), `PolarClient.Oauth2.Token` (POST). Handle OAuth code → token exchange. Honor `OnboardingOptions.Server` (Sandbox/Production).
- **Acceptance:** Wizard end-to-end test against sandbox provisions a real org, captures real OAT (once-readable), registers a real webhook endpoint, returns populated `OnboardedTenantResult`. EfMultiTenantStoreSink persists it; subsequent webhook delivery against the new tenant succeeds.

### TASK-V20-007 — Reconcile FakeDataSyncService toggle branches

- **Status:** Deferred → v2.0 (depends on TASK-V20-001)
- **Owner:** unassigned
- **Project:** PolarSharp.DataSeeding
- **Context:** `AllowFakeData` OFF↔ON toggle fires `FakeDataToggleChanged`; the service listens but the OFF→ON publish and ON→OFF archive branches are stubbed pending the catalog publisher's HTTP wiring.
- **What to do:** Once TASK-V20-001 lands, replace stub branches with `IPolarCatalogPublisher.PublishAsync(scope=AllFakeData)` and `ArchiveAllAsync(predicate: x => x.IsFakeData)`. Confirm `Metadata["polar_sharp_is_fake_data"]="true"` is set on every published fake record so the snapshot ingester preserves the marker.
- **Acceptance:** Integration test: seed fake data with `AllowFakeData=true`, flip to false, assert all fake products in Polar sandbox are archived; flip back to true, assert they're un-archived. No `IsFakeData=false` records touched.

### TASK-V20-008 — Add RLS DDL to initial migrations for SqlServer + PostgreSQL

- **Status:** Deferred → v2.0
- **Owner:** unassigned
- **Project:** PolarSharp.MultiTenant.EntityFrameworkCore.{SqlServer,PostgreSQL} + identity/catalog/reporting equivalents
- **Context:** The plan calls for database-layer Row-Level Security on every tenant-owned table (Layer 2 of the 5-layer cross-tenant safeguard). v1.2.0 ships the EF query filter (Layer 1) and the session interceptors (`SqlServerTenantSessionInterceptor`, `PostgreSqlTenantSessionInterceptor`) but the initial migrations do NOT add `CREATE SECURITY POLICY` (SqlServer) or `ENABLE ROW LEVEL SECURITY` + `FORCE ROW LEVEL SECURITY` + `CREATE POLICY` (PostgreSQL).
- **What to do:** Add raw-SQL DDL via `migrationBuilder.Sql(...)` in each initial migration. For SqlServer, create the `tenant_filter` table-valued function + `tenant_security_policy` SECURITY POLICY with FILTER + BLOCK predicates on every `ITenantOwned` table. For PostgreSQL, enable + force RLS and add the `tenant_isolation` policy referencing `current_setting('app.current_tenant_id', true)` and `current_setting('app.is_app_master_admin', true)`. The session interceptor already sets both session vars per request.
- **Acceptance:** Add a `Category=RlsBypass` test that opens a raw `SqlConnection` / `NpgsqlConnection` (bypassing EF), sets the session var to Tenant A, attempts to SELECT WHERE tenant_id = Tenant B, asserts zero rows returned. Re-run with `is_app_master_admin=true` and assert the query returns rows (AppMasterAdmin bypass confirmed).

### TASK-V20-009 — Commission extensive integration tests

- **Status:** Standing commission for v2.0 (user-requested 2026-05-13)
- **Owner:** unassigned
- **Project:** Polar.sh_Nuget — all packages
- **Context:** v1.2.0 ships 441 unit tests; integration coverage against Polar's sandbox API is light because HTTP wiring is deferred (TASKs V20-001 through V20-006). As each HTTP wire-up task above lands, the corresponding integration test suite must be filled out.
- **What to do:** As a continuing commission alongside each TASK-V20-001..006: write `[Trait("Category","Integration")]` tests against Polar sandbox for the live flow. Coverage targets:
  - Onboarding: programmatic + OAuth + wizard end-to-end (TASK-V20-006)
  - Catalog publish: idempotency, partial-failure resume, variant + tier expansion, dependency order (TASK-V20-001)
  - Refunds: full, partial, listing (TASK-V20-002) ✅ done
  - License validation: valid, expired-in-grace, revoked, max-activations (TASK-V20-003) ✅ done
  - Business profile: PATCH + payout poller (TASK-V20-004) ✅ done
  - Reporting snapshot: idempotency, checkpoint advance, pre-aggregate accuracy (TASK-V20-005)
  - Fake-data toggle sync: OFF→ON publish + ON→OFF archive against real sandbox (TASK-V20-007)
  - RLS bypass: raw-connection cross-tenant read blocked (TASK-V20-008)
  - KeyCloak SSO: full OIDC flow against a KeyCloak Testcontainer
  - EF Core provider matrix: per-provider isolation, migration idempotency, health-check states
- **Acceptance:** Each TASK-V20-xxx above lists at least one integration test as part of its own acceptance; the umbrella commission is satisfied when those tests exist and run green in CI's `integration-test` job (separate from the unit-test gate, gated by sandbox credentials).

---

## v1.3.0 — Missing Service Implementations ✅ SHIPPED 2026-05-13

All 8 sub-phases (TASK-V13-001..008) shipped together as v1.3.0 (tagged + released; see `CHANGELOG.md [1.3.0]`). The following summary records closure; per-task line-items are no longer maintained.

- **TASK-V13-001** — `EfTranslationProviderResolver` + `AddTranslationResolver()`. Done.
- **TASK-V13-002** — `EfCatalogRepository` + `EfTranslationRepository` + `PolarCatalogReader`. Done.
- **TASK-V13-003** — `RefundService` + `LicenseKeyValidator` (with caching + grace period). Done.
- **TASK-V13-004** — `PolarBusinessProfileService` (incl. `BuildBankingSetupDeepLink`) + `InventoryUpdater` (zero-boundary `SkuStockChanged` events). Done.
- **TASK-V13-005** — `PolarCatalogPublisher` (variant + tier expansion, dependency-order, idempotency, resume-from-OutOfSync). Done.
- **TASK-V13-006** — `ReportSnapshotService` (paginated polling, checkpoint advance, pre-aggregate recompute). Done. Plus 12 advanced reports (8 tenant + 4 SaaS-operator) atop the snapshot tables.
- **TASK-V13-007** — `AddPolarEcommerce()` + `AddPolarReporting()` orchestrator extensions. Done.
- **TASK-V13-008** — Release: PolarSharp / Webhooks / MultiTenant bumped 1.2.1 → 1.3.0; CHANGELOG [1.3.0] published; DocFX articles authored (refund-management, license-validation, business-profile, inventory, publisher, snapshot-service, advanced-reporting); tag `v1.3.0` pushed.

**Note:** the HTTP wires for the new services still call deferred-stub Polar adapters (TASK-V20-001..006 remain open). v1.3.0 is "feature-complete at the abstraction + orchestration layer" — the live Polar HTTP plumbing under those orchestrators ships with v2.0. See `CHANGELOG.md` line 30 + below for the v2.0 task list.

---

## v1.4.0 — Storefronts + Wallet (in progress)

### TASK-V14-001 — Storefront core services (Phase 25) ✅ SHIPPED to main 2026-05-19

Cart / Checkout / Customer services + GuestSessions package + idempotency cache + cart expiry + guest-to-customer cart promotion. 89 unit tests across two new test projects. Documentation: `docs/articles/storefronts-cart-checkout.md` + Implementation Narrative + per-package READMEs. See `CHANGELOG.md [Unreleased]`.

### TASK-V14-002 — Wallet event store (Phase 20) ✅ SHIPPED to main 2026-05-20 (PR #4)

`PolarSharp.PrepaidWallets.Abstractions` (events, commands, queries, value objects, interfaces). `PolarSharp.PrepaidWallets` core domain (aggregate, handlers, behaviors, in-memory stores). EF Core + Marten event-store providers. Funding-source provenance (`FundingSourceKind` enum + `FundingSourceAllocation`) on every funding/credit/debit event per the WTR coordination note (see PLAN.md). Tenant_id indexes on EF provider migrations. DocFX article + Implementation Narrative. 124 wallet tests across 4 test projects; all green. **NOT YET IN CHANGELOG `[Unreleased]`** — gap; close before tagging v1.4.0.

### TASK-V14-003 — 17 pipeline-stage skeletons (Phase 26)

- **Status:** Not started
- **Project:** `PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing` (~6 stages) + `.SubscriptionBilling` (~6) + `.RefundProcessing` (~5)
- **Problem:** Phase 25 (cart/checkout core) shipped real services that hand off to the order-processing pipeline. Phase 26's stages — `QuoteTaxStage`, `ApplyDiscountsStage`, `FulfillStage`, etc. — are currently log-and-pass-through stubs. `StorefrontScaffoldDiagnosticService` flags this gap at startup (LogLevel.Warning) but there's no tracking task.
- **What to build:** Real implementations per stage. Quote tax via `IStorefrontTaxProvider`. Validate discount codes server-side per Case Study 03 fraud-prevention discipline. Fulfillment hook to inventory + shipping. Subscription billing cycle handlers. Refund-flow stages.
- **Acceptance:** Each stage has its own unit-test class + at least one integration test that drives a realistic checkout through the full pipeline. `StorefrontScaffoldDiagnosticService` no longer warns about Phase 26 scaffolds.
- **References:** `src/PolarSharp.EcommerceStorefronts.Pipelines.*/Stages/*.cs`; `CHANGELOG.md` Phase 25 [Unreleased] note.

### TASK-V14-004 — Wallet Phase 20 entry in CHANGELOG `[Unreleased]` ✅ DONE 2026-05-20

- **Status:** Closed. CHANGELOG `[Unreleased]` now carries the Wallet event-store subsection (PrepaidWallets.Abstractions + core + EF Core / Marten providers + funding-source provenance + tenant_id indexing + 124 tests + DocFX article cross-ref). Authored in this session's audit-pass commit.

### TASK-V14-006 — Fix CI Integration job to target the correct test projects ✅ DONE 2026-05-20

- **Status:** Closed. `.github/workflows/ci.yml` Integration step previously ran `dotnet test tests/PolarSharp.IntegrationTests --filter Category=Integration`, which has zero matching tests. Fixed to run `tests/PolarSharp.EcommerceStoreManagement.Tests` + `tests/PolarSharp.Reporting.Tests` (the projects that actually own the live-sandbox tests for V20-002/003/004/005).
- **Acceptance verified:** On a clean push, CI's "Integration Tests (sandbox)" step now actually executes the ~16 live-Polar tests (when `POLAR_SANDBOX_TOKEN` is provisioned as a GitHub Actions secret).

### TASK-V14-007 — Acquire credentials + upgrade SkippableFact-gated AI provider tests to always-running

- **Status:** Pending — REQUIRES USER ACTION (acquire credentials for 4 AI providers)
- **Priority:** HIGH — blocks "v1.4.0 testing fully and accurately represents state"
- **Project:** `tests/PolarSharp.EcommerceStoreManagement.Translation.Tests/LiveProviderIntegrationTests.cs`
- **Context:** As of 2026-05-20 the live AI translation provider tests are SkippableFact-gated on per-provider API key env vars. Locally and in CI today they skip because credentials are not yet provisioned. User has committed to acquiring credentials for all five providers (Anthropic / OpenAI / Azure OpenAI / Gemini / Grok); once received we circle back and implement the FULL set of tests instead of relying on the [SkippableFact] flags.
- **What to do (once credentials are in hand):**
  1. Add the following env vars to .env (and direnv) for local dev: `ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `AZURE_OPENAI_API_KEY` + `AZURE_OPENAI_ENDPOINT` + `AZURE_OPENAI_DEPLOYMENT`, `GEMINI_API_KEY`, `GROK_API_KEY`.
  2. Add the same set as GitHub Actions repository secrets so the CI Integration job can run them.
  3. Extend `.github/workflows/ci.yml` Integration step to also run `tests/PolarSharp.EcommerceStoreManagement.Translation.Tests --filter Category=Integration` with those env vars.
  4. Rewrite `LiveProviderIntegrationTests.cs` to drop the SkippableFact gates and assert against real provider responses unconditionally.
  5. Expand coverage beyond the current single-field roundtrip — multi-field translations, larger payloads, error-path coverage (rate limit / auth fail / network timeout).
- **Acceptance:** All 5 providers run live tests in CI on every push to main; tests fail loudly on any HTTP wire-contract regression; coverage extends beyond single-field roundtrip.

### TASK-V14-008 — Wallet EventStore EFC provider implementations (Phase 21.x)

- **Status:** Pending — Phase 21.x; 5 scaffold packages currently marked IsPackable=false
- **Priority:** HIGH — blocks "wallet event persistence works on user-chosen provider"
- **Project:** `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.{Sqlite, SqlServer, PostgreSQL, MariaDb, CosmosDb}`
- **Context:** The 5 provider packages currently have a single extension file each (the `UseXxxWalletEventStore` method) that's a no-op returning `services` unchanged. XML doc explicitly says "Phase 21 ships the registration scaffold; full DbContext + migrations land in Phase 21.x." The base `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore` package has real implementation (`EfWalletEventStore`, generic `WalletEventStoreDbContext`, `WalletEventRecord`, `WalletSnapshotRecord`, `BucketsJsonCodec`); only the per-provider plumbing is missing.
- **What to do:**
  1. Per provider: implement a provider-specific `XxxWalletEventStoreDbContext` (or wire the generic one with `UseXxx()`).
  2. Generate initial migrations including the `ix_wallet_events_tenant_id_occurred_at` index (per WTR coordination + DECISIONS.md D-003).
  3. Provider-specific quirks: MariaDb needs `MariaDbCompatibleHistoryRepository` (Oracle-provider `GET_LOCK` workaround); Cosmos needs `/walletId` partition key + aggressive snapshot threshold per the XML doc; SQLite needs per-tenant `.db` file factory.
  4. Drop `IsPackable=false` from each csproj as it gains real content.
  5. Per-provider Testcontainers integration tests covering: schema creation, append-then-load round-trip, idempotency replay, optimistic-concurrency conflict, cross-tenant isolation. Mirror the pattern in `tests/PolarSharp.MultiTenant.EntityFrameworkCore.Tests/Integration/`.
- **Acceptance:** All 5 providers ship real implementations + paired integration tests; scaffold integrity test no longer counts these as scaffolds; CHANGELOG corrected to reflect honest scope.

### TASK-V14-009 — Wallet Polar bridge implementations (Phase 22.x)

- **Status:** Pending — Phase 22.x; 4 scaffold packages currently marked IsPackable=false
- **Priority:** HIGH — blocks "wallet has Polar.sh interop"
- **Project:** `PolarSharp.PrepaidWallets.Polar.{Checkout, GraphQL, Identity, Reporting}` + `PolarSharp.PrepaidWallets.Reporting`
- **Context:** Per the subagent audit on 2026-05-20, each of the 4 Polar.* bridges is a 25-28 line scaffold file. XML doc on each says "Phase 22 ships the bridge package shell; full impl lands in Phase 22.x" and enumerates the expected types: `PolarWalletCheckoutInterceptor`, `PolarWalletRefundConverter`, `PolarWalletSubscriptionDebitor`, identity-wiring (`ICurrentUser → IWalletIdentityProvider`), reporting + audit log `SaveChangesInterceptor`, GraphQL type extensions.
- **What to build:**
  - `PolarSharp.PrepaidWallets.Polar.Checkout` — `PolarWalletCheckoutInterceptor` (wallet-only + hybrid modes per WTR design), `PolarWalletRefundConverter` (Polar refund → wallet credit), `PolarWalletSubscriptionDebitor` (renewal-time wallet debit if wallet covers subscription).
  - `PolarSharp.PrepaidWallets.Polar.Identity` — `ICurrentUser → IWalletIdentityProvider` adapter so wallet aggregate gets the current authenticated user.
  - `PolarSharp.PrepaidWallets.Polar.Reporting` — audit log `SaveChangesInterceptor` wiring; reporting integration so wallet events flow to the snapshot tables.
  - `PolarSharp.PrepaidWallets.Polar.GraphQL` — wallet-aware GraphQL type extensions, audience-scoped.
  - `PolarSharp.PrepaidWallets.Reporting` (the abstraction) — concrete `IWalletReportingClient` implementation (projection-backed).
  - Drop `IsPackable=false` on each as it gains real content.
- **Tests:** Per bridge, integration tests against the live Polar sandbox (gated on `POLAR_SANDBOX_TOKEN` via SkippableFact). Specifically:
  - Wallet-only checkout: customer with wallet balance covers full purchase; Polar.sh never sees the transaction; assert wallet debit + tenant tax obligation (per WTR Phase 22.5 framework).
  - Hybrid checkout: wallet covers part, Polar covers rest; assert correct split.
  - Refund-to-wallet: Polar refund credited back as `RefundAsCredit` wallet event.
- **Acceptance:** Each bridge ships real implementation + paired tests; scaffold integrity test no longer counts these as scaffolds; CHANGELOG corrected.

### TASK-V14-010 — Wallet read-model projections (Marten projection daemon)

- **Status:** Not started — HIGH priority; blocks Phase 22.5 WTR framework's `SaaSTaxOwedReport` and `TenantTaxOwedReport`
- **Project:** New `PolarSharp.PrepaidWallets.Projections` package (lift-safe core) + `PolarSharp.PrepaidWallets.Polar.Projections` (Polar-specific projections)
- **Problem:** The wallet has aggregate-internal projections (`WalletAggregate.Apply` rebuilds state on load) + snapshots (every N events to avoid replaying the full stream), but ZERO read-model projections — denormalized views that queries / UIs / reports consume directly. Today any query like "show me this customer's wallet transaction history" or "give me the tenant's wallet revenue this quarter" requires `LoadAsync(walletId, fromSeq=1)` + in-memory materialization. Won't scale; doesn't support cross-wallet aggregation. Phase 22.5 WTR's `saas_revenue_ledger` (per PLAN.md D-002 design) is exactly this kind of projection but it's not built.
- **What to build:**
  - **`WalletBalanceSummary` projection** — one row per (tenantId, walletId, currency) with current balance, status, last-activity timestamp. Reads `WalletOpened` / `WalletFunded` / `WalletDebited` / `WalletCredited` / `WalletRefunded` / `WalletClosed` events. Used by tenant-admin dashboards.
  - **`WalletTransactionHistory` projection** — one row per (walletId, sequenceNo) with denormalized event payload + funding-source allocation. Used by customer-facing wallet history pages.
  - **`TenantWalletRevenueLedger` projection** — Phase 22.5's `saas_revenue_ledger` table; one row per recognized-revenue event with tenant_id, jurisdiction, revenue_type, amount_cents, recognized_at_utc. The source-of-truth for the WTR tax reports.
  - **`CustomerLifetimeWalletValue` projection** — cross-wallet aggregation per customer (a customer may have multiple wallets across tenants if the SaaS supports it). Powers the customer-360 view.
  - **Marten implementation** uses the Marten-native primitives per DECISIONS.md D-008 "Marten implementations MUST leverage Marten-native event-sourcing features": projection daemon for async projections, `MultiStreamProjection<T>` for cross-stream views like `TenantWalletRevenueLedger`, `InlineProjection` only where transactional consistency with the event append is REQUIRED, event-metadata indexing on `tenant_id` instead of denormalised columns, tenancy-aware document stores. Refactor the current `MartenWalletEventStore` (which today treats Marten as Postgres-with-streams) to use `AggregateStreamAsync<WalletAggregate>` + native `ExpectedVersion` concurrency instead of the manual stream-read + version-check it does today.
  - **EF Core implementation** uses a polling-based replay runner (less feature-rich than Marten's daemon but works on every provider). Per-provider variants live in `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.*` (currently scaffolds; need to land alongside TASK-V14-008).
  - Per-provider Testcontainer integration tests covering: projection catches up from event #0 on fresh start, projection updates on new events, projection is idempotent on replay.
- **Acceptance:** All four projections ship; replay-from-zero works against real Postgres (Marten) + EF Core providers; sub-millisecond reads from denormalized tables verified; Marten implementation uses native primitives per D-008 (audit-checked by a code-review pass before merge).
- **References:** DECISIONS.md D-008 (event-sourced aggregates + projections + Marten-native usage); PLAN.md "Wallet Tax Responsibility (WTR) framework" Component 5.

### TASK-V14-011 — Gift cards as their own event-sourced aggregate

- **Status:** Not started — HIGH priority; current treatment is incomplete
- **Project:** New packages `PolarSharp.GiftCards.Abstractions` (lift-safe core) + `PolarSharp.GiftCards` (aggregate + behaviors) + `PolarSharp.GiftCards.Polar.Checkout` (bridge for redemption at Polar checkout)
- **Problem:** Today gift cards are treated as a *wallet funding source* — when a card is redeemed, a `WalletFunded(Source: GiftCardActivation)` event lands on the recipient's wallet stream and that's the entire record. This works for "redeemed-on-arrival" cards but loses crucial gift-card lifecycle history:
  - Activation date + amount + purchaser + intended recipient
  - Partial redemptions across multiple wallets / sessions
  - Card transfers (gifter → recipient → secondary recipient)
  - Card expiry + escheatment (unclaimed-property laws by jurisdiction)
  - Card replacement (lost / stolen / damaged)
  - Gift-card-specific tax treatment (most US states: redemption is the taxable event; activation is NOT)
- **What to build:**
  - `GiftCardAggregate` with event stream: `GiftCardActivated`, `GiftCardRedeemed` (partial or full), `GiftCardTransferred`, `GiftCardExpired`, `GiftCardReplaced`, `GiftCardEscheated`
  - Wallet events reference the gift card by id (`Option<Guid> SourceGiftCardId` on `WalletFunded`); the gift card's own event stream tracks the card's lifecycle independently
  - Aggregate-internal projections: current remaining balance, redemption history
  - Read-model projections: `GiftCardSummaryPerTenant` (active cards + total outstanding liability — important for tenant accounting), `ExpiringGiftCardsAlerts` (cards within N days of expiry; tenant lifecycle notification trigger)
  - Polar-bridge: `PolarSharp.GiftCards.Polar.Checkout` registers a checkout interceptor that recognises gift-card redemption codes at the Polar checkout step + emits `GiftCardRedeemed` + `WalletFunded` events transactionally
  - Two end-to-end test scenarios: (1) "issue card → recipient redeems part for purchase A → recipient gifts the remainder to a different person who redeems for purchase B"; (2) "issue card → card expires unclaimed → escheatment to the appropriate jurisdiction's unclaimed-property fund"
- **Acceptance:** Gift card lifecycle queryable independently of wallet events; tenant accounting can report outstanding-gift-card-liability accurately; Polar checkout redemption flows end-to-end against the sandbox.

### TASK-V14-012 — Loyalty + referrals as event-sourced aggregates

- **Status:** Not started — MEDIUM priority; design unblocks WC catalog tasks in PLAN.md Phase 3
- **Project:** New packages `PolarSharp.LoyaltyAccounts.Abstractions` + `PolarSharp.LoyaltyAccounts` + `PolarSharp.LoyaltyAccounts.Polar.*` and parallel `PolarSharp.Referrals.*` family
- **Problem:** Same problem as gift cards — these are long-lived, immutable-history-preferred, balance-tracking things treated today as wallet funding-source stamps (`TenantPromotionalGrant` on a wallet event). Loyalty programs need:
  - Point accrual events per qualifying purchase
  - Tier progression (Bronze → Silver → Gold based on cumulative-points-this-year)
  - Point expiry rules (typically rolling 12-24 months)
  - Tier downgrade rules (when annual qualifying-points falls below threshold)
  - Reward redemption events (`LoyaltyPointsRedeemed`) feed wallets via `WalletFunded(Source: TenantPromotionalGrant)`
- **Referrals need:**
  - Attribution events (referee signs up via referrer's link)
  - Attribution-window rules (referrer earns X% of referee's purchases for first N days)
  - Multi-step chains (referrer → referee → referee-of-referee, with N-deep payout schedules per program design)
  - Reward issuance events feeding wallets
- **What to build:**
  - `LoyaltyAccountAggregate` + `ReferralAggregate` each with their own event streams + aggregate-internal projections + read-model projections + per-provider Testcontainer tests
  - Polar bridges for the redemption / payout paths
  - WCs that consume the projections: `polar-loyalty-tier-badge`, `polar-referral-link-share`, `polar-referee-earnings-summary` (per PLAN.md Phase 3 WC catalog — these were placeholder entries; this task gives them real backing)
- **Acceptance:** Loyalty + referral lifecycles queryable independently of wallet event streams; WCs render against real projection data.

### TASK-V14-005 — Wallet event-store Implementation Narrative

- **Status:** Not started
- **Project:** `docs/articles/narratives/`
- **Problem:** CLAUDE.md mandates an Implementation Narrative for every major workflow change. The Phase 20 wallet event store shipped a DocFX article (`prepaid-wallets-event-sourcing.md`) but no narrative. The CHANGELOG `[Unreleased]` entry currently flags this as pending.
- **What to do:** Write `docs/articles/narratives/wallet-event-store-for-saas-operators.md` following the Implementation Narratives writing-style rules in CLAUDE.md (audience-friendly, concrete scenarios, analogies, short paragraphs). Cover: what a prepaid wallet IS in plain language, why event sourcing (audit trail + replay), what funding-source provenance does for the SaaS operator (the tax story without lecturing), the FIFO debit allocation walkthrough, and "things to know" at the end (refund-as-credit, gift-card activation as a redemption-time taxable event, etc.). Add to `docs/articles/toc.yml` under Implementation Narratives.

---

## v2.0 — Honest deferrals from v1.3.0 + new feature designs (added 2026-05-13 during v1.3.H pre-commit pass)

### TASK-V20-010 — Tenant store clone / export-import (new feature)

- **Status:** Not started — v2.0
- **Project:** New optional package `PolarSharp.EcommerceStoreManagement.Migration` (with EF Core provider sub-packages — SqlServer / Sqlite / PostgreSQL — only if the export format differs by provider; the JSON format itself is provider-agnostic)
- **Problem:** A tenant wants to clone their entire store into another PolarSharp-driven environment — typically when forking a sandbox into production, or migrating from one host deployment to another. Manual re-entry of products / categories / benefits / discounts / checkout links / translations is hours of work and error-prone.
- **What to build:**
  - `ITenantExporter.ExportAsync(TenantId tenantId, ExportOptions options, Stream output, CancellationToken ct)` — serialises every `ITenantOwned` row for the tenant to JSON (default) or CSV-zipped (option). Schema version embedded in the envelope. Excludes the Polar-side ids (`PolarProductId`, `PolarBenefitId`, etc.) so the import side gets a clean "republish from scratch" state. Includes / excludes `IsFakeData=true` rows via `ExportOptions.IncludeFakeData` (default false).
  - `ITenantImporter.ImportAsync(TenantId targetTenantId, Stream input, ImportOptions options, CancellationToken ct)` — replays the export into a fresh tenant. Validates schema version, validates `targetTenantId` is empty (or merges per `ImportOptions.ConflictPolicy`), regenerates internal ids, re-runs `IPolarCatalogPublisher.PublishAsync` afterwards so the new tenant has a fresh Polar org wired up.
  - JSON shape: `{ "schemaVersion": "1.0", "tenantId": "...", "exportedAt": "...", "products": [...], "categories": [...], "benefits": [...], "discounts": [...], "checkoutLinks": [...], "translations": [...], "businessProfile": {...} }`. NOT included: AuditLog (history is per-deployment), Reporting snapshots (rebuilt by snapshot service), Identity memberships (per-deployment users), Polar-side ids.
  - Encrypted-at-rest fields (translation API key on `TenantBusinessProfile`) re-encrypted with the target environment's key ring at import time. The export bundles only the encrypted blob plus a marker that says "needs re-encryption against target keyring"; if the target lacks the source's data-protection keys (the common cross-environment case), the field is dropped at import and the tenant must re-enter via the business-profile UI.
  - `[RequirePolarPermission(ExportTenantData)]` gates export; `[RequireAppMasterAdmin]` plus `[AllowCrossTenant]` gates import.
- **Acceptance:** Round-trip test (export → import into a fresh in-memory tenant → assert all rows present and Polar-ids cleared). Schema-version-mismatch rejection test. Conflict-policy test. Cross-environment data-protection-keyring test (encrypted fields dropped + warning logged).
- **Coupled with TASK-V20-011** (wipe): both features ship together so a tenant can "export, wipe, re-onboard, import" as one workflow.

### TASK-V20-011 — Tenant store reset / wipe with safeguards (new feature)

- **Status:** Not started — v2.0
- **Project:** New `ITenantWipeService` in `PolarSharp.EcommerceStoreManagement` core + EF impl in `.EntityFrameworkCore`
- **Problem:** A tenant wants to blow away their entire ecommerce setup — typically after a botched onboarding or a major model migration — and start fresh. Currently the host has to write tenant-row DELETE statements by hand across 12+ tables, with no audit trail and no rollback safety.
- **What to build:**
  - `ITenantWipeService.PreviewAsync(TenantId tenantId, CancellationToken ct)` — returns a `TenantWipePreview { ProductRowCount, CategoryRowCount, BenefitRowCount, DiscountRowCount, CheckoutLinkRowCount, TranslationRowCount, AuditLogRowCount, ... }` so the host's confirmation dialog shows exactly what will be deleted.
  - `ITenantWipeService.WipeAsync(TenantId tenantId, TenantWipeRequest request, CancellationToken ct)` with structured safeguards on `TenantWipeRequest`:
    1. **`ConfirmationToken`** — single-use token previously issued by `RequestConfirmationTokenAsync(tenantId)`; expires in 5 minutes; usable once
    2. **`TypedTenantSlug`** — caller types the tenant's slug verbatim; service rejects on mismatch
    3. **`AcknowledgedNoRollback : true`** — explicit boolean; defaults to false; service rejects on false
    4. **`AutoExportBeforeWipe : true`** (default) — service runs `ITenantExporter.ExportAsync` first and writes the result to `request.ExportBackupSink` BEFORE deleting anything. Failure to write the backup blocks the wipe. The host can opt out (`AutoExportBeforeWipe=false`) only with an explicit `BypassExportReason` string the audit log captures
  - Authorization: `[RequireAppMasterAdmin]` PLUS `[AllowCrossTenant]` PLUS optional `[RequireMfaWithinLastNMinutes(15)]`. NO tenant-scoped role can wipe — even `TenantAdmin` lacks this permission.
  - Audit: writes BOTH the tenant's `AdminAuditLogEntry` AND the platform-level `PlatformAuditLogEntry` with `Action=Delete`, `EntityType="Tenant"`, full snapshot of the row counts deleted, the typed slug, the actor identity, the optional `BypassExportReason`, and (if `AutoExportBeforeWipe=true`) a reference to the backup blob location
  - Polar-side cleanup: optional `request.AlsoArchiveInPolar : bool` — if true, every published product / benefit / discount / checkout link is `is_archived: true`'d in Polar before local DELETE. Polar has no DELETE, so the post-wipe state in Polar is "archived but inspectable for compliance." If false, Polar rows are orphaned (the tenant's Polar organization keeps the products visible until manually cleaned via Polar's dashboard)
  - **Hard impossibility:** wipe cannot be invoked from any tenant-scoped API surface, no matter what permission flag is held. The endpoint is on a platform-admin route ONLY. The `ITenantWipeService` interface itself throws `InvalidOperationException` if resolved from a scope where `ICurrentUser.IsAppMasterAdmin == false`
- **Acceptance:** Confirmation-token expiry test (token issued 6 minutes ago → rejected). Slug-mismatch test. `AcknowledgedNoRollback=false` rejection test. AutoExportBeforeWipe failure-to-write rejection test. Audit-trail dual-write test (tenant log + platform log). Service-resolution-blocked-from-tenant-scope test.
- **Coupled with TASK-V20-010** (clone): the auto-export uses the export feature. Tasks ship together.

### TASK-V20-012 — RLS DDL actually in initial migrations (UPGRADE from TASK-V20-008)

- **Status:** Not started — v2.0 (P1 from production-readiness audit)
- **Problem:** `TenantAwareDbContextBase.cs:28-32` documents a "layer 2 defense" via SQL Server / PostgreSQL Row-Level Security policies, but `grep -rn "CREATE POLICY|ENABLE ROW LEVEL|sp_set_session_context" src` returns zero hits. The migrations don't include the RLS DDL. Today's only line of defense is the EF query filter.
- **What to do:** Generate proper RLS migrations for SQL Server (`CREATE SECURITY POLICY tenant_security_policy ...`) and PostgreSQL (`ALTER TABLE ... ENABLE ROW LEVEL SECURITY; CREATE POLICY tenant_isolation ON ... USING (tenant_id::text = current_setting('app.current_tenant_id', true))`). Wire connection interceptors that set the session variable on every connection open. Add cross-tenant raw-SQL bypass tests proving RLS blocks the read at the database layer.
- **See:** `PRODUCTION-READINESS-ANALYSIS.md` for the full audit context.

### TASK-V20-013 — AuditLogSaveChangesInterceptor (actually exist) ✅ DONE 2026-05-19

- **Status:** Done. The 2026-05-19 codebase stub audit (Section 3 #2) flagged this task as stale: the interceptor IS implemented (225-line `SaveChangesInterceptor` at `src/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore/AuditLogSaveChangesInterceptor.cs`), registered Scoped in `CatalogServicesExtensions`, and attached via `AddInterceptors` in all 5 provider extensions: `SqliteCatalogBuilderExtensions`, `SqlServerCatalogBuilderExtensions`, `PostgreSqlCatalogBuilderExtensions`, `MariaDbCatalogBuilderExtensions`, `CosmosDbCatalogBuilderExtensions`. Status updated to reflect reality.
- **Original problem (resolved):** `AdminAuditLogEntry.cs:7-9` referenced an `AuditLogSaveChangesInterceptor` that didn't exist at the time of the production-readiness audit. Now it does.
- **Implementation:** `SaveChangesInterceptor.SavingChanges*` inspects `ChangeTracker.Entries<ITenantOwned>()`, captures before/after values via `entry.OriginalValues` / `entry.CurrentValues`, and emits `AdminAuditLogEntry` rows in the same transaction (per the original spec).
- **Outstanding:** No paired regression test was identified during the audit verifying that a mutation through a path WITHOUT explicit `auditLog.AddAsync(...)` still captures the entry via the interceptor. Optional follow-up.
- **See:** `PRODUCTION-READINESS-ANALYSIS.md` for the original audit context.

### TASK-V20-014 — Restore server-side query translation on SQL Server / PostgreSQL

- **Status:** Not started — v2.0 (P1 at scale, from production-readiness audit)
- **Problem:** `EfAdvancedReportingClient` uniformly applies a materialise-then-filter pattern to dodge a SQLite limitation. On SQL Server / PostgreSQL the same code materialises entire orders / customers / events tables client-side on every advanced report. At scale this is unviable.
- **What to do:** Either (a) introduce a provider-specific override path so SQL Server / PostgreSQL keep the filter server-side while SQLite falls back to materialise-then-filter, or (b) rewrite the queries with `ToLocalTime()` or `DateTime` boundary conversions that all three providers can translate. Approach (a) is cleaner; approach (b) is more invasive but provider-uniform.
- **See:** `PRODUCTION-READINESS-ANALYSIS.md` for the full audit context.

### TASK-V20-015 — Top 10 v2.0 priorities from PRODUCTION-READINESS-ANALYSIS.md

- **Status:** Not started — v2.0 (multi-item umbrella)
- **What to do:** Walk the "Top 10 priorities for v2.0" section of `PRODUCTION-READINESS-ANALYSIS.md` and seed each as its own TASK-V20-NNN entry once the v2.0 cycle begins. Document already exists at `/Users/mollsandhersh/Repos/Polar.sh_Nuget/PRODUCTION-READINESS-ANALYSIS.md` with file:line references and severity ratings.

### TASK-V20-016 — Bump GitHub Actions to Node 24

- **Status:** Not started — v2.0 (maintenance; deadline 2026-06-02 before runners force-upgrade)
- **Problem:** v1.3.0 CI run surfaced deprecation warnings on `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/upload-artifact@v4`. GitHub forces Node 24 by default on 2026-06-02; Node 20 removed from runners 2026-09-16.
- **What to do:** Bump the three actions in `.github/workflows/ci.yml` and `.github/workflows/docs.yml` to the Node-24-compatible versions (likely `@v5` or whatever's current at the time). Validate with a tag run.

### TASK-V20-017 — Litestream CLI implementation (`polar-mt litestream init|verify`)

- **Status:** Not started — v2.0 (Stage C scaffolds present; bodies deferred)
- **Project:** `PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite`
- **Problem:** `src/PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite/Litestream/LitestreamCliCommands.cs` carries two methods (`Init`, `VerifyAsync`) that throw `NotImplementedException` with a previously stale "v1.2.x+1" marker. The Stage C deliverable shipped the shape + contract only; the actual command bodies + `System.CommandLine` discovery / dispatch were deferred.
- **What to build:**
  - `Init`: invoke `LitestreamConfigGenerator.Generate(...)` to produce `litestream.yml` from resolved `LitestreamOptions` + the SQLite database directory; `File.WriteAllText(outputPath, ...)`. Return exit code 0 on success, non-zero on validation failure.
  - `VerifyAsync`: enumerate replicated `.db` files, restore each to a temp directory via `litestream restore`, open with `Microsoft.Data.Sqlite`, run `PRAGMA integrity_check`, report pass/fail summary. Useful for periodic DR rehearsal.
  - Wire a `System.CommandLine` root command under the `polar-mt litestream <verb>` namespace; expose as the package's `dotnet tool` install target.
- **Acceptance:** Unit tests for `Init` (config-generation success + write-failure paths). Integration test for `VerifyAsync` that boots a Litestream-replicating SQLite fixture, captures replicas, restores them, and asserts integrity-check passes. CLI discovery test: `polar-mt litestream --help` returns the expected verbs.

### TASK-V20-018 — Cross-pod distributed snapshot dedup (per-tenant)

- **Status:** Not started — v2.x (deferred from V20-005)
- **Project:** `PolarSharp.Reporting` (snapshot orchestrator)
- **Problem:** `PerTenantSnapshotOrchestrator` uses an in-process `SemaphoreSlim` per tenant. Multi-pod hosts (web farm) get cross-pod overlap windows where two pods independently fetch the same tenant's snapshot. Polar GET idempotency makes this wasteful, not destructive, so it's tolerable for v1.
- **What to build:** Pluggable `IDistributedSnapshotLock` abstraction with a `RedisDistributedSnapshotLock` reference impl (Redis SETNX + TTL). Wire into the orchestrator so per-tenant snapshots dedup across pods. Single-pod hosts continue using the in-process default.
- **Acceptance:** Integration test with two `PerTenantSnapshotOrchestrator` instances sharing a Redis (testcontainer); attempt simultaneous tenant ticks; assert only one outbound HTTP path runs.
- **See:** `docs/archive/DESIGN-V20-005-PER-TENANT-SNAPSHOT.md` for original framing.

### TASK-V20-019 — Webhook payload capture + offline analyzer

- **Status:** Approved 2026-05-14 (spec at `/Users/mollsandhersh/Repos/Polar.sh_Nuget/DESIGN-V20-019-WEBHOOK-CAPTURE-AND-ANALYZER.md`); not started
- **Scheduled:** v2.0 Pillar 2 (production hardening), after V20-005 Phase 1B–1H complete
- **Problem:** PolarSharp webhook tests today use synthetic-but-cryptographically-real payloads (we generate the JSON + HMAC-sign locally). We never confirm that the JSON Polar actually sends matches what our `WebhookXxxData` records expect. Polar can add fields, deprecate fields, ship new event types, or change enum values and our tests stay green because they're testing themselves.
- **Solution:** Capture verified webhook payloads to a sink (file system / DB / blob), run an analyzer over the corpus offline, report on coverage / schema drift / unknown event types / handler-coverage gaps / field-value statistics. Wire as CI pre-publish gate.

- **Implementation phases (from the design doc):**
  - **2A** — `IWebhookPayloadCapture` interface in core + `NoOpWebhookPayloadCapture` default + pipeline integration (post-verify, pre-dispatch) + opt-in config plumbing. ~2 hours
  - **2B** — `PolarSharp.Webhooks.Capture.FileSystem` package: write impl + prune `IHostedService` + integration test. ~2 hours
  - **2C** — `PolarSharp.Webhooks.Capture.EntityFrameworkCore` base + 3 SQL providers + entity + config + 3 migrations + prune job. ~4 hours
  - **2D** — `PolarSharp.Webhooks.Analyzer` library: coverage report + schema-drift via reflection against `WebhookXxxData` records + unknown-event detection + handler-coverage check + field-value stats. ~4 hours
  - **2E** — `dotnet polarsharp-webhook-analyze` CLI tool + JSON output + `--fail-on-unknown` / `--fail-on-drift` exit codes. ~2 hours
  - **2F** — Corpus-replay test pattern + synthesized fixture set (≥1 sample per `WebhookXxxData` type) + CI workflow step invoking the analyzer. ~2 hours
  - **2G** — `PolarSharp.Webhooks.Capture.AzureBlob` and `.S3` packages. Deferred to v2.x. ~3 hours
  - **2H** — Docs: `docs/articles/webhook-payload-capture.md` (privacy + setup + retention) + `docs/articles/webhook-corpus-analysis.md` (analyzer usage + CI integration). ~1 hour

- **Must-have set for v2.0:** 2A + 2B + 2D + 2F + 2H (~12 hours). 2C (DB sink) and 2E (CLI tool) are highly desirable; 2G defers to v2.x.

- **Privacy invariants (mandatory; spelled out in the design doc):**
  - All sink packages disabled by default; hosts opt in via `PolarSharp:Webhooks:Capture:Enabled=true`
  - Even when enabled, only explicitly-listed event types capture
  - Existing `IPolarPiiRedactor` invoked on raw JSON before write (emails / names / addresses redacted automatically)
  - 7-day default retention with daily prune `IHostedService`
  - **Fingerprint-only mode** (schema-only, no values) safe for indefinite retention — recommended default per the design's open-question lean
  - Repo-committed `tests/.../Corpus/` samples MUST be synthesized — never derived from real captures without anonymisation

- **Acceptance:** synthetic fixture set committed; analyzer reports correctly identify a missing handler / unknown event type / dropped field in a fixture-driven test; CI step invokes analyzer with appropriate `--fail-on-*` flags; documentation articles explain privacy defaults and operator opt-in flow.

- **Open questions deferred to implementation time:** see the design doc's "Open questions for project owner" section (4 items: fingerprint-default, CI gate on drift, DB-sink package boundary, anonymisation tool).
