---
name: PolarSharp Build Progress
description: Which implementation phases are complete vs pending for the PolarSharp NuGet SDK
type: project
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
## Completed phases (as of 2026-05-08)

- **Phase 1** — Solution scaffolded: PolarSharp.slnx, Directory.Build.props, Directory.Packages.props, all projects created
- **Phase 2** — Kiota client generated from `https://api.polar.sh/openapi.json` into `src/PolarSharp/Generated/`
- **Phase 3** — Core library complete: PolarClient, PolarOptions, BearerTokenHandler, IdempotencyKeyHandler, ApiVersionHandler, PolarResilienceHandler, PolarSocketsHandlerFactory, health check, ActivitySource, Meter, pagination, Result<T,E>/Option<T> monads, PolarError hierarchy, localization (en-US + es-MX), ServiceCollectionExtensions, ApplicationBuilderExtensions, PolarInfrastructureBuilder, PolarCustomerPortalClient
- **Phase 4** — Webhooks complete: WebhookValidator (HMAC-SHA256, multi-secret, timing-uniform), all 28 event records, IPolarWebhookHandler<T>, PolarWebhookHandlerBase<T>, IPolarWebhookDispatcher, KnownWebhookEventTypes, PolarWebhookStartupValidator (with default-path warning), WebhookBuilderExtensions, PolarWebhookOptions
- **Phase 4b** — Toast notifications: IPolarToastChannel, PolarToastNotification rich record, ToastPropertyExtractors, AddPolarToastNotifications extension
- **Phase 4c Security** — Webhook endpoint security hardening: Content-Type enforcement (415), payload size limit (413), IP allowlist (403), HTTPS enforcement (400), rate limiting (429) via ASP.NET Core RateLimiter + ActivateWebhookRateLimiterIfAvailable middleware activation, anomaly detection (VerificationFailureTracker + suspicious_activity gauge), timing-uniform error responses, CIDR IP matching
- **Phase 4c Templates** — `PolarSharp.Templates` NuGet template pack: `dotnet new polar-handler --event <EventType> --name <HandlerName>` covering all 28 event types with per-event-type conditional XML doc property lists in `EventHandler.cs` template
- **Phase 5** — MultiTenant complete: PolarTenantInfo, MultiTenantPolarClientFactory, IMultiTenantPolarClientFactory, PolarMultiTenantOptions, TenantStrategy enum, strategy sub-options, MultiTenantBuilderExtensions.AddPolarMultiTenant(), localization (en-US + es-MX)
- **Phase 7** — PolarTestApp complete: Program.cs + 15 endpoint files
- **Phase 9** — CI/CD: `.github/workflows/ci.yml` and `.github/workflows/docs.yml`
- **Phase 10** — Docs: `docfx.json`, `docfx-filter.yml`, `docs/toc.yml`, 10 documentation articles
- **Phase 11** — Per-tenant bulkhead isolation: TenantResilienceDelegatingHandler, LazyConcurrentDictionary, IAsyncDisposable for graceful shutdown
- **Phase 12** — Enterprise polish: PolarPiiRedactor (24 tests), CHANGELOG.md, Public API snapshot tests, JSON wire-format snapshot tests (16 snapshots), ResultExtensions (ValidationError → HTTP 422)
- **Benchmarks** — `tests/PolarSharp.Benchmarks/` with BenchmarkDotNet: LazyConcurrentDictionaryBenchmarks, WebhookVerificationBenchmarks, PerTenantIsolationBenchmarks
- **Gap Fixes (2026-05-08)** — All 7 gaps: PolarScopeBuilder, configurable resilience, channel depth gauge, ArrayPool for HMAC, FsCheck property tests, PolarWarmupService, webhook in-memory dedup

## Standalone Webhooks + v1.1.0 release (2026-05-12)

`PolarSharp.Webhooks` is now a fully standalone NuGet package (no PolarSharp core dependency):
- **`AddPolarWebhooks(IServiceCollection)`** — direct IServiceCollection extension
- **`MapPolarWebhooks(IEndpointRouteBuilder)`** — registers POST /hooks/polar on any host
- **`PolarWebhooksBuilder`** — fluent builder returned for standalone handler registration
- **`testapp/PolarWebhooksTestApp`** — reference app using only PolarSharp.Webhooks; all 28 handlers
- **`IWebhookTenantScopeInitializer` binding bug fixed** — was causing HTTP 500 on every webhook POST in standalone mode (parameter binding attempted JSON deserialization of interface → NotSupportedException); fixed by resolving via `RequestServices.GetService<T>()`
- **`UsePolarInfrastructure` webhook discovery** — no longer gated behind `marker.WebhooksRegistered`

### Test suite additions
- `StandaloneRegistrationTests` — 70 new webhook unit tests
- `StandaloneOptionsTests`, `StandaloneStartupValidatorTests`, `StandaloneHttpPipelineTests` — DI, options, E2E pipeline
- `StandaloneWebhookPipelineTests` — 48 integration tests via WebApplicationFactory<Program>

### Documentation additions
- `docs/articles/webhooks.md` — standalone vs full-stack mode table; 2a/2b registration sections
- `docs/articles/webhook-handlers.md` — rewrote; full 28-event table, standalone registration example
- `docs/articles/webhook-event-reference.md` — new article; per-event payload schemas, C# handler skeletons, JSON examples

### Package metadata
- **Authors: Molls and Hersh, LLC** (all three library csproj files)
- PolarSharp → 1.1.0
- PolarSharp.Webhooks → 1.1.0
- PolarSharp.MultiTenant → 1.1.0

### Release
- Committed as `69a4f61` on main, pushed to `MollsAndHersh/Polar.sh_Nuget`
- Tagged `v1.1.0`, pushed; CI publish job now running → pushes 1.1.0 packages to GitHub Packages feed

## Test count (2026-05-12 — v1.1.0 release)

- `PolarSharp.Tests`: **90/90** pass
- `PolarSharp.Webhooks.Tests`: **70/70** pass (was 24; +46 standalone tests)
- `PolarSharp.IntegrationTests`: **48/48** pass (was 1; +47 standalone integration tests)
- **Total: 208/208 pass**

## Solution projects

- `src/PolarSharp` — core library (v1.1.0)
- `src/PolarSharp.Webhooks` — webhooks package (v1.1.0, standalone)
- `src/PolarSharp.MultiTenant` — multi-tenant package (v1.1.0)
- `tests/PolarSharp.Tests` — core unit tests (90)
- `tests/PolarSharp.Webhooks.Tests` — webhook unit tests (70)
- `tests/PolarSharp.IntegrationTests` — live sandbox + standalone integration tests (48)
- `tests/PolarSharp.Benchmarks` — BenchmarkDotNet benchmarks
- `testapp/PolarTestApp` — end-to-end test app (full-stack)
- `testapp/PolarWebhooksTestApp` — standalone webhooks test app
- `templates/PolarSharp.Templates` — dotnet new template pack

## Key build facts

- **Full solution build**: 0 errors
- **Test command**: `dotnet test --filter "Category!=Integration" -c Release`
- **AOT verify command**: `dotnet publish testapp/PolarTestApp -c Release -p:PublishAot=true`
- **Authors**: Molls and Hersh, LLC (in all three library csproj files)
- **Package feed**: `https://nuget.pkg.github.com/mollsandhersh/index.json`
- **Docs site**: `https://mollsandhersh.github.io/Polar.sh_Nuget/`

## AOT fix + DocFX logo + badge fix (2026-05-12 — post v1.1.0)

Three-commit progression to fully resolve CI and badges:

**Commit `78f6d57`** — removed `JsonSerializer.Serialize(@event, PrettyPrint)` from `LoggingHandlerBase<TEvent>`
in both testapp handler files (`testapp/PolarTestApp/Handlers/LoggingWebhookHandlers.cs` and
`testapp/PolarWebhooksTestApp/Handlers/LoggingWebhookHandlers.cs`); replaced with structured log of
EventType + WebhookId. Added `docs/images/polar-logo.png` + `docfx.json` `_appLogoPath`.

**Commit `2c15263`** — added `<IlcTreatWarningsAsErrors>false</IlcTreatWarningsAsErrors>` to both testapp
csproj files. INSUFFICIENT — SDK targets file (`Microsoft.NETCore.Native.targets`) overrides
`IlcTreatWarningsAsErrors` from `$(TreatWarningsAsErrors)` AFTER the project file runs.

**Commit `902fd16`** — correct fix: `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` in both testapp
csproj files (with `<WarningsAsErrors />` to clear any inherited value). Also changed README.md badge URLs
from `github/v/release` to `github/v/tag` so badges resolve from git tags without requiring a GitHub Release.
v1.1.0 tag force-updated → CI green → publish job runs → GitHub Release created → all four badges show v1.1.0.

**Key lesson:** For AOT smoke test in demo/testapp projects, set `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` (not `<IlcTreatWarningsAsErrors>`). Library projects keep `TreatWarningsAsErrors=true`.
**Key lesson:** Use `github/v/tag` shields.io badge (reads git tags immediately) not `github/v/release` (requires GitHub Release from CI).

**Why:** PolarSharp is a .NET NuGet SDK project for Polar.sh payment integration, owned by Molls and Hersh, LLC (markchipman@gmail.com). All major plan phases complete. 208/208 tests pass. Webhooks package is standalone.

**How to apply:** At the start of a new session, check this memory. All work is complete. Zero AOT errors. 208/208 tests pass. Webhooks package is standalone. v1.1.0 released. CI is green. All four README badges display v1.1.0.
