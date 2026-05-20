# PROGRESS.md

Authoritative progress log. Record completed tasks, verification results, RAG indexing notes, and Zoran compliance status for .NET/C# work.

---

## 2026-05-20 — Post-v1.3.0 reconciliation + audit pass

### Current state on `main` (after the 2026-05-20 testing overhaul)

- **Tip:** post-overhaul (see git log; the audit pass + testing overhaul commits)
- **Build:** `dotnet build PolarSharp.slnx -c Release` → 0 warnings, 0 errors across 103 src projects
- **Test suite:** **1164 passing, 13 skipped, 0 failed** across 25 test projects (full slnx-wide `dotnet test`).
  - Net delta from the pre-overhaul baseline (1163/8/0): +2 scaffold integrity tests, –1 deleted `UnitTest1.cs` placeholder in `PolarSharp.IntegrationTests`, +5 new `LiveProviderIntegrationTests.cs` covering all 5 AI translation providers.
  - Of the 13 skipped: 8 are `CosmosDbSingleTenantUpgradeMigratorIntegrationTests` (`[SkippableFact]` when Cosmos emulator unavailable) and 5 are the new AI provider live tests (`[SkippableFact]` until project owner supplies credentials — TASK-V14-007).
- **Honest reporting note:** as of this overhaul, "Passed" actually means "ran and passed." Previously, ~16 live-Polar sandbox tests silently no-op'd when `POLAR_SANDBOX_TOKEN` was unset and reported as Passed — those are now SkippableFact-gated and report as Skipped honestly. See `TESTING.md` for the full breakdown.

### Realignment of stale planning docs

The previous PROGRESS.md entry was frozen at 2026-05-13 (the v1.2.1 / v1.3.0 kickoff session). Substantial work has since landed unrecorded:

- **v1.3.0 released** — see `CHANGELOG.md [1.3.0] — 2026-05-13`. All 8 v1.3.0 sub-phases (TASK-V13-001..008) shipped: refunds, license validation, business profile, inventory updater, catalog publisher, snapshot service, orchestrator extensions, plus 12 advanced reports.
- **v1.4.0 Phase 25 (Storefronts core)** merged 2026-05-19 — `DefaultStorefrontCartService`, `DefaultStorefrontCheckoutService`, `DefaultStorefrontCustomerService`, `GuestSessions` package with signed-cookie roundtrip + middleware; idempotency cache, cart expiry, guest-cart promotion. 89 unit tests across two new test projects.
- **v1.4.0 Phase 20 (Wallet event store)** merged 2026-05-20 as PR #4 — `PolarSharp.PrepaidWallets.Abstractions` + core domain + EF Core / Marten event-store providers + funding-source provenance per the WTR coordination note. 124 wallet tests across 4 test projects.
- **Phase 25 follow-ups** merged 2026-05-19 — idempotency replay coverage, cart-expiry tests, guest-cart promotion tests, narrowed `StorefrontScaffoldDiagnosticService` to flag only the remaining Phase 26 scaffolds.
- **slnx fix** (commit `9472798`) — registered 4 wallet test projects so the solution-wide `dotnet test` sweep picks them up.

### Audit pass cleanups (this session)

- **Stale design docs archived** — moved `COORDINATION-NOTE-FROM-MAIN-SESSION.md`, `DESIGN-V20-005-PER-TENANT-SNAPSHOT.md`, `DESIGN-V20-019-WEBHOOK-CAPTURE-AND-ANALYZER.md`, `DESIGN-V20-020-MEMORY-LEAK-AUDIT.md`, `PrepaidWalletsLiftAndShift.md` into `docs/archive/`. Closed-work artifacts no longer pollute the repo root.
- **TASKS.md realigned** — collapsed the 8 v1.3.0 sub-phase entries into a single closed summary block; added TASK-V14-001..004 covering Phase 25 (shipped), Phase 20 (shipped), Phase 26 pipeline stages (open), and the missing wallet CHANGELOG entry; added TASK-V20-017 (Litestream CLI) and TASK-V20-018 (cross-pod snapshot dedup) to fill task-id gaps that were previously orphaned in code + archived design docs.
- **Litestream CLI stub markers refreshed** — `LitestreamCliCommands.Init` + `VerifyAsync` previously threw `NotImplementedException` with a stale "v1.2.x+1" deferral marker. Updated to reference TASK-V20-017.
- **Templates version bump** — `PolarSharp.Templates.csproj` was at 1.2.1; bumped to 1.3.0 to match the rest of the v1.3.0 family (PolarSharp / Webhooks / MultiTenant).
- **DECISIONS.md populated** — was 3 lines. Promoted seven locked architectural decisions from PLAN.md / Case Studies / coordination notes: Polar.sh-as-MoR framing, WTR framework, Phase 20 funding-source event provenance, lift-and-shift namespace convention, 5-layer tenant isolation, agent-coordination protocol, three-package UI (.Core / .Web / .Maui) split.

### Audit findings deferred (not in-session)

Two findings are tracked but not addressed in this session because both are multi-session efforts:

- **CS1591 opt-outs in 35 packages** — `Directory.Build.props` mandates CS1591 as a build-error, but 35 packages (mostly the Storefronts family) suppress it via `<NoWarn>CS1591</NoWarn>`. Closing this gap requires adding XML doc coverage to those packages' public surface. Recommended approach: pick one representative package (likely `PolarSharp.EcommerceStorefronts.AspNetCore`), drop the suppression, add docs to satisfy the gate, then propagate the recipe across the remaining 34 in a dedicated docs-sweep phase.
- **65 src packages with no paired test project** — notable test-less families: all Storefronts Polar bridges + Pipelines + Themes + SEO + Search + Shipping + Tax + WebComponents, all CustomerGraph + NaturalLanguageQuery packages, all EventStore EFC provider variants, both Marten reporting/onboarding bridges. Recommended approach: prioritize the Storefronts Polar bridges + Pipelines first since they're core to v1.4.0; add tests one package at a time as parallel agent work behind the existing 5-layer tenant isolation acceptance gate.

---

## 2026-05-13 — PolarSharp v1.2.0 + v1.2.1 release, docs fixes, v1.3.0 kickoff

### v1.2.0 (released)

- **Phases 1–11 implemented** across 13 commits on feature branch, merged to `main` as `3a3ebc7` (no-ff)
- **25 new packages added**: BaseEntities, MultiTenant.EntityFrameworkCore (+ 3 providers), MultiTenant.Identity (+ 3 providers + KeyCloak), Onboarding, EcommerceStoreManagement (+ EF + 3 providers + 3 translation providers), Reporting (+ EF + 3 providers), DataSeeding. Translation.Gemini and Translation.Grok scaffolded but not yet packed in CI (fixed in v1.2.1)
- **12 EF Core migration sets** (4 DbContexts × 3 providers) + `PolarMigrationRunner<TContext>` hosted service
- **441/441 tests passing**
- **CI publish run #26 successful** against `v1.2.0` tag, all 27 packages packed and pushed to GitHub Packages `nuget.pkg.github.com/mollsandhersh/`

### v1.2.1 (released, commit `ccac427`)

- **CI workflow gap fixed**: added Translation.Gemini + Translation.Grok pack steps (they shipped as v1.2.0 packages but were missing from `ci.yml`)
- **DocFX template switch** from classic to `["default", "modern"]` — fixed missing top navbar on the docs site
- **Version bump** PolarSharp + Webhooks + MultiTenant + Templates 1.2.0 → 1.2.1
- **Tag `v1.2.1` pushed**; CI publish run #28 successful
- All 29 packages now live in GitHub Packages (27 from v1.2.0 + 2 from v1.2.1)

### Docs site recovery (commit `43ee830` then `532a814`)

- Repo owner flipped GitHub Pages source from "Deploy from a branch" (which was serving Jekyll-rendered `docs/index.md` with default theme) to "GitHub Actions"
- Added `workflow_dispatch:` to `docs.yml` for future manual reruns
- Rewrote `docs/index.md` to mirror the v1.2.0+ ecosystem framing from the README — 7-row capability table, expanded install snippets, v1.2.0 callout, updated Features table
- Live docs site now correctly serves the DocFX-built site with modern template + Articles/API Reference top-nav: `https://mollsandhersh.github.io/Polar.sh_Nuget/`

### README content refresh (commits `7929cab`, `b6a2df7`)

- Replaced 4 misleading per-package badges (all rendered the same repo tag) with a single Latest-release badge + a collapsible `<details>` block containing version badges for all 31 packages grouped by capability area
- Added Publish Docs workflow status badge alongside CI
- Rewrote "What You Get" table from 3-package framing to 7-row capability-area framing
- Expanded install snippets to cover the v1.2.0+ packages
- Added "Banking and payouts" subsection to the EcommerceStoreManagement Packages-at-a-Glance entry, plus 7 new article links in the Documentation table

### Stripe / banking clarification (commit `1347e01`)

- Project owner requested plain-language clarification across all three doc surfaces that PolarSharp does NOT call Stripe — the merchant sets up their bank account in Polar.sh's own dashboard, Polar uses Stripe internally to move money, but the host application has no Stripe relationship at all
- Updated XML docs on `IPolarBusinessProfileService.BuildBankingSetupDeepLink` and `RefreshPayoutStatusAsync` with non-technical explanations
- Added "Banking and payouts — why PolarSharp can't do this for you" section to `docs/articles/ecommerce-catalog.md`
- Added a clarifying paragraph to the README's EcommerceStoreManagement Packages-at-a-Glance entry
- **Note:** the agentic-master commit wrapper also auto-staged the untracked `src/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore/Translation/EfTranslationProviderResolver.cs` (148 lines) — that file is the start of v1.3.A and ended up bundled with the doc commit. The implementation compiles clean; future v1.3.A commits will add tests + DI registration

### v1.3.0 kickoff

- **Discovery:** about half the v1.2.0 service surface (`IRefundService`, `ILicenseKeyValidator`, `IPolarCatalogPublisher`, `IPolarBusinessProfileService`, `IInventoryUpdater`, `IPolarCatalogReader`, `ITranslationProviderResolver`, `IReportSnapshotService`, `ICatalogRepository`, `ITranslationRepository`, the orchestrator-flavored `IPolarCatalogTranslator`) ships as interfaces only — no concrete implementations. Documented in `PLAN.md`.
- **v1.3.0 scope agreed with project owner:** build the missing implementations + orchestrator extensions (`AddPolarEcommerce()`, `AddPolarReporting()`) as 8 sub-phases totaling ~3900 LOC. v1.4.0 then refreshes the test apps on the solid v1.3.0 foundation.
- **Sub-phase 1.3.A status:** `EfTranslationProviderResolver` implementation written and compiles clean (committed inadvertently in `1347e01`). Tests + DI extension still pending. Next session resumes here.
- **TASKS.md updated** with TASK-V13-001 through TASK-V13-008 covering the 8 sub-phases.

### Verification on current main

- `dotnet build` → clean, 0 warnings, 0 errors (verified post-v1.2.1)
- `dotnet test` → 441/441 (no new tests added since v1.2.1)
- `dotnet build src/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore -c Release` → clean (verified with the new resolver file present)

### v1.3.H complete — audits + 12 advanced reports + 7 docs + v2.0 strategy (2026-05-13)

**Scope delivered against project owner's explicit direction "Yes — ship all 12 reports + docs + analysis + release":**

1. **12 advanced reports shipped** in `PolarSharp.Reporting` + `PolarSharp.Reporting.EntityFrameworkCore`:
   - 8 tenant reports: RevenueOverTime, TopProducts, TopCustomers, SubscriptionChurnCohort, RefundRate, AverageOrderValue, CustomerLifetimeValueDistribution, CurrencyMix
   - 4 SaaS-operator reports: CrossTenantRevenue, CrossTenantOrderVolume, WebhookDeliveryHealth, TenantHealth
   - 12/12 unit tests passing on top of existing reporting suite
   - Materialise-then-filter pattern applied uniformly to dodge SQLite's DateTimeOffset translation gap; flagged in v2.0 production-readiness analysis (production providers should restore server-side translation)

2. **Three audits performed pre-commit (per project owner's explicit request):**
   - Audit 1 (incomplete impls): clean. All stubs map to tracked TASK-V20-001..007 deferrals. No unintentional dropped work
   - Audit 2 (test gaps): 4 quick wins closed in this commit (`tests/PolarSharp.Reporting.Tests/QuickWinGapTests.cs`); 3 v2.0 deferrals (FakeDataSync concurrency, per-provider tenant isolation, InventoryUpdater races) documented in PLAN.md
   - Audit 3 (documentation): CS1591 build-error gate confirms 100% XML doc coverage; 7 missing v1.3.0 articles authored; CHANGELOG v1.3.0 entry written; README package count briefly mis-corrected 31 → 30 then reverted back to 31 in a v1.3.0 follow-up commit after the publish log confirmed `PolarSharp.Templates` is the 31st package (it lives at `templates/`, not `src/`)

3. **Seven DocFX articles authored** at `docs/articles/`: refund-management.md, license-validation.md, business-profile.md, inventory.md, publisher.md, snapshot-service.md, advanced-reporting.md. All linked from `toc.yml`

4. **Production-readiness analysis delivered** at `/Users/mollsandhersh/Repos/Polar.sh_Nuget/PRODUCTION-READINESS-ANALYSIS.md` (~4800 words, 8 sections, 20+ proposed TASK-V20-NNN entries, top-10 priorities ranked). Surfaced consequential gaps including:
   - RLS layer documented but absent from migrations (only EF query filter today)
   - `AuditLogSaveChangesInterceptor` referenced in comments but doesn't exist
   - `EfAdvancedReportingClient` materialises client-side uniformly (unviable at scale on SQL Server / PostgreSQL)
   - No transaction boundary on multi-step catalog publishes (crash mid-publish loses Polar-id mapping)
   - Webhook background queue drops events without DLQ

5. **Two new feature designs from project owner's mid-task surface requests:**
   - **Clone (TASK-V20-010):** export/import tenant catalog as JSON; round-trip across PolarSharp deployments
   - **Wipe (TASK-V20-011):** single-use confirmation token + typed-slug verification + AppMasterAdmin-only + dual audit-log writes
   - Both designed and added to TASKS.md as coupled v2.0 features

6. **v2.0 launch strategy appended to PLAN.md** — three pillars (HTTP completion, production hardening, clone+wipe) plus branching strategy plus 6–8 week target cadence

7. **CHANGELOG.md v1.3.0 entry** complete; **version bumps** PolarSharp/Webhooks/MultiTenant 1.2.1 → 1.3.0

**Test suite state on commit:** quick-win tests (9 new) all green; advanced reporting tests (12) all green; full reporting suite (41 tests in this project) green; broader repo suite green on last verification.
