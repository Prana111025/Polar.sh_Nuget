# PROGRESS.md

Authoritative progress log. Record completed tasks, verification results, RAG indexing notes, and Zoran compliance status for .NET/C# work.

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
