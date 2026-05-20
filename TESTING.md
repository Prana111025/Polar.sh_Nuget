# TESTING.md

How tests are structured in this repo, what each layer proves, and where the gaps are. **Read this before trusting a passing test count.**

The headline counts on `main` as of 2026-05-20 (after the testing overhaul):

- **`dotnet test PolarSharp.slnx`** with `POLAR_SANDBOX_TOKEN` + valid AI provider keys set locally: ~1170 passed / ~20 skipped / 0 failed across 25 test projects.
- **`dotnet test PolarSharp.slnx`** with no credentials set: same total, but the 16 live-Polar tests + 5 live-AI-provider tests all report as **Skipped** (no longer silently Passed — the 2026-05-20 SkippableFact conversion enforces honest reporting).
- **CI**: "Build, Test & AOT" step runs ~1147 tests (Layer 1 + 3 + 4 + scaffold integrity). "Integration Tests (sandbox)" step now correctly runs the EcommerceStoreManagement.Tests + Reporting.Tests live-Polar tests with `POLAR_SANDBOX_TOKEN` (the 2026-05-20 CI fix corrected the wrong-project-path bug).

Both numbers are accurate. Neither one alone is "everything is tested" — different test layers prove different things.

---

## The four test layers

### Layer 1 — Domain / unit tests (the bulk)

**What:** ~900 tests across most test projects in `tests/`. Pure C# domain logic against fakes, in-memory stores, or test fixtures. No network, no real database, no external state.

**Examples:**
- `tests/PolarSharp.EcommerceStorefronts.Tests/` — uses `FakeCatalogProvider`, `FakeTimeProvider`, `TestPipelineBuilder`, `CompletingStage`, `TestStorefrontIdentityProvider`, `TestGuestSessionAccessor`. Verifies cart fraud-prevention semantics, checkout snapshot creation, customer-auth gating against test doubles.
- `tests/PolarSharp.PrepaidWallets.Tests/` — `InMemoryStoreTests`, `WalletAggregateTests`, `FifoBucketAllocationTests`, `CommandHandlerTests`, `SnapshotEquivalenceTests`, `JsonSerializerTests`. All in-memory; no Polar interop.
- `tests/PolarSharp.BaseEntities.Tests/`, `tests/PolarSharp.MultiTenant.Tests/`, etc.

**What they prove:** the C# code runs correctly given test-controlled inputs. Refactoring safety. Public-API contract stability.

**What they don't prove:** that Polar.sh's actual responses match what the wrappers expect; that EF migrations work against real databases; that production runtime composition resolves correctly.

**How to run:** `dotnet test --filter "Category!=Integration"` — the CI "Build, Test & AOT" step runs exactly this.

### Layer 2 — Polar HTTP wrapper tests (live sandbox)

**What:** ~16 test classes that hit `https://sandbox-api.polar.sh` via `PolarClient` and verify the request/response mapping. Each class is tagged `[Trait("Category", "Integration")]`.

**Where they live:**
- `tests/PolarSharp.EcommerceStoreManagement.Tests/PolarClientRefundsApiIntegrationTests.cs` — V20-002
- `tests/PolarSharp.EcommerceStoreManagement.Tests/PolarClientLicenseKeysApiIntegrationTests.cs` — V20-003
- `tests/PolarSharp.EcommerceStoreManagement.Tests/PolarClientOrganizationsApiIntegrationTests.cs` — V20-004
- `tests/PolarSharp.Reporting.Tests/PolarClientReportingApi*IntegrationTests.cs` — 12 files covering events, orders, subscriptions, customers, benefit-grants, products, customer-meters, license-keys, benefits, meters, checkout-links, discounts (V20-005)
- `tests/PolarSharp.Reporting.Tests/SnapshotIdempotencyIntegrationTests.cs` — V20-005 idempotency proof

**Gated on:** the `POLAR_SANDBOX_TOKEN` environment variable via `[SkippableFact]` (as of 2026-05-20). When the variable is unset, every test in this layer reports as **Skipped** with the message `POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test`:

```csharp
[SkippableFact]
public async Task ListRefundsForOrder_returns_success_when_no_refunds_match()
{
    Skip.If(string.IsNullOrEmpty(Token), "POLAR_SANDBOX_TOKEN not set — skipping live Polar sandbox test");
    // … real assertions follow only when Token is present
}
```

**Historical gotcha (fixed 2026-05-20):** before the SkippableFact conversion, every test in this layer used `if (string.IsNullOrEmpty(Token)) return;` which reported as Passed without making a network call. The fix replaces silent-pass with honest Skipped reporting so green status now means "actually ran" rather than "actually ran OR token was missing."

**What they prove (when actually executing):** the Polar HTTP roundtrip works for V20-002/003/004/005 — request shaping, response parsing, error-status mapping (404 → typed error, 400 → typed error variants, 5xx → UnexpectedFailure).

**What they don't prove:** the V20-001 catalog publisher (which still returns stub `UnexpectedFailure` per `PolarClientPublishingApi.cs:40`), the V20-006 onboarding (which still throws via `StubKiotaPolarOnboardingApi`), or anything else not explicitly in the V20-002..005 set above.

**How to run locally:**

```sh
# direnv autoloads POLAR_SANDBOX_TOKEN from .env if you have it
export POLAR_SANDBOX_TOKEN=polar_sandbox_xxx
dotnet test tests/PolarSharp.EcommerceStoreManagement.Tests --filter "Category=Integration"
dotnet test tests/PolarSharp.Reporting.Tests --filter "Category=Integration"
```

**To audit which tests in this layer actually ran (vs skipped):** xUnit's runner output marks Skipped tests with `[SKIP]` so the audit is trivial: `dotnet test --filter Category=Integration` prints the verdict for each test.

### Layer 2b — Live AI translation provider tests (added 2026-05-20)

**What:** 5 tests in `tests/PolarSharp.EcommerceStoreManagement.Translation.Tests/LiveProviderIntegrationTests.cs` covering each shipped AI translation provider: Anthropic, OpenAI, Azure OpenAI, Gemini, Grok. Each test makes a real HTTP call to the provider's API and verifies the request shape + response parsing for a single-field translation.

**Gated on:** per-provider API key env vars via `[SkippableFact]`:
- `ANTHROPIC_API_KEY`
- `OPENAI_API_KEY`
- `AZURE_OPENAI_API_KEY` + `AZURE_OPENAI_ENDPOINT` + `AZURE_OPENAI_DEPLOYMENT` (Azure requires all three)
- `GEMINI_API_KEY`
- `GROK_API_KEY`

**Also gracefully skips when credentials are present but rejected** (401/403) — so a stale env var doesn't fail the suite. The Skip message identifies which provider rejected the creds so the contributor knows to rotate the key.

**Forward commitment (TASK-V14-007):** once the project owner supplies valid credentials for all five providers (currently only `POLAR_SANDBOX_TOKEN` and a 401-returning `OPENAI_API_KEY` exist locally), the SkippableFact gates will be dropped and these tests become always-running live verifications.

### Layer 3 — In-process pipeline / WebApplicationFactory tests

**What:** ~48 tests in `tests/PolarSharp.IntegrationTests/Standalone/StandaloneWebhookPipelineTests.cs`. These boot the real `PolarWebhooksTestApp` host inside a `WebApplicationFactory<TEntryPoint>` and exercise the full ASP.NET Core webhook pipeline: middleware, routing, DI, HMAC verification, event dispatch.

**Payloads:** synthetic — generated locally with valid HMAC signatures. NOT real Polar webhook deliveries.

**Why "Integration" in the project name then?** Historical naming. These are in-process integration tests (multiple components running together inside one host), not external-service integration tests. They do not require any environment variables; they do not call Polar.sh.

**What they prove:** the webhook pipeline routes all 28 event types correctly, HMAC verification works end-to-end, the standalone webhooks package works in isolation (zero dependency on multi-tenant or core SDK).

**What they don't prove:** that Polar's actual webhook payloads still match `WebhookXxxData` shapes (covered by TASK-V20-019: webhook payload capture + offline analyzer).

**How to run:** included in Layer 1's `Category!=Integration` filter — runs in CI's "Build, Test & AOT" step automatically.

### Layer 4 — Testcontainers / EF provider tests

**What:** ~70 tests across `tests/PolarSharp.MultiTenant.EntityFrameworkCore.Tests/Integration/` covering migration idempotency + cross-tenant isolation against real SqlServer / PostgreSQL / MariaDB / Cosmos instances via Testcontainers.

**Skipped tests:** 8 Cosmos tests use `[SkippableFact]` and Skip.If when the Cosmos Linux emulator can't boot. Locally + most CI machines = skip. On machines with the emulator available, they run.

**What they prove:** EF migrations apply cleanly per provider; the multi-tenant query filter blocks cross-tenant reads at the EF layer; provider-specific session interceptors set the expected variables.

**What they don't prove:** RLS (Row-Level Security) at the database layer — that's TASK-V20-008 / V20-012, still open. Today's only line of defense is the EF query filter (Layer 1 of the 5-layer tenant isolation per DECISIONS.md D-005).

---

## How CI runs the tests

`.github/workflows/ci.yml` has two test-running jobs:

1. **`Build, Test & AOT`** (`if: github.event_name == 'push' && github.ref == 'refs/heads/main'`)
   - `dotnet test --filter "Category!=Integration"` — runs Layers 1 + 3 + 4 across the entire `.slnx`.
   - Build with CS1591 WarningsAsErrors enforced, vulnerability scan, AOT publish smoke test on `PolarTestApp`.

2. **`Integration Tests (sandbox)`** (`if: github.event_name == 'push' && github.ref == 'refs/heads/main'`)
   - As of 2026-05-20, runs the two package-specific test projects that actually own the live-Polar tests:
     ```sh
     dotnet test tests/PolarSharp.EcommerceStoreManagement.Tests --filter "Category=Integration"
     dotnet test tests/PolarSharp.Reporting.Tests --filter "Category=Integration"
     ```
   - Supplied with `POLAR_SANDBOX_TOKEN` as a GitHub Actions secret. With the secret set, the V20-002/003/004/005 live-sandbox tests actually execute; without it (e.g. forks), they Skip.
   - **Historical bug (fixed 2026-05-20):** previously this step ran `dotnet test tests/PolarSharp.IntegrationTests --filter "Category=Integration"`. That project has zero `[Trait("Category","Integration")]` tagged tests, so the step exited "successfully" with `No test matches the given testcase filter` and ran zero tests on every CI run for an extended period. The fix targets the correct projects.

The CI tag-trigger publish job runs only when a `v*` tag is pushed (`if: startsWith(github.ref, 'refs/tags/v')`).

---

## Known gaps

| Gap | Layer | Severity | Tracking |
|---|---|---|---|
| ✅ **Fixed 2026-05-20** — CI Integration job runs the correct projects | 2 | (closed) | (closed) |
| ✅ **Fixed 2026-05-20** — Silent-skip on missing token replaced with SkippableFact | 2 | (closed) | (closed) |
| ✅ **Fixed 2026-05-20** — 17 packable-but-scaffold packages correctly marked `IsPackable=false` so they can't ship empty to NuGet | 1 | (closed) | (closed) — caught by `ScaffoldIntegrityTests` going forward |
| AI translation provider tests Skip without per-provider credentials | 2b | Medium — provider HTTP wire contracts not verified for Anthropic / Azure / Gemini / Grok in CI | **TASK-V14-007** (forward commitment to upgrade once project owner supplies credentials) |
| 17 Phase 26 pipeline stages still log-and-pass-through stubs | 1 | High — `StorefrontScaffoldDiagnosticService` flags them at startup; cart/checkout downstream is non-functional | **TASK-V14-003** |
| Wallet EventStore EFC providers (5 packages) still scaffolds — DbContexts + migrations not implemented | 1 | High — wallet event persistence works on the base EFC package + Marten only, not on the 5 provider-specific packages | **TASK-V14-008** (Phase 21.x — real per-provider implementations) |
| Wallet Polar bridges (4 packages: Checkout / GraphQL / Identity / Reporting) still scaffolds | 1 | High — wallet has zero Polar.sh interop today | **TASK-V14-009** (Phase 22.x — real bridge implementations) |
| Real webhook payloads never compared to `WebhookXxxData` shapes | 3 | Medium — synthetic payloads alone don't catch Polar schema drift | **TASK-V20-019** (webhook capture + analyzer) |
| RLS not in migrations | 4 | High — cross-tenant isolation at DB layer doesn't exist; EF query filter is the only defense | **TASK-V20-008 / V20-012** |
| 8 Cosmos integration tests skip on local machines | 4 | Low — covered when emulator available; covered by SkippableFact pattern | None — by design |

---

## Quick reference: testing each package family

| Package family | Test coverage | What you can trust |
|---|---|---|
| PolarSharp core SDK, Webhooks, BaseEntities, MultiTenant | Layer 1 + 3 + (4 for MultiTenant EFC providers) | Domain logic + webhook pipeline + provider migrations + cross-tenant filter |
| EcommerceStoreManagement (refunds, license, business profile, inventory, publisher minus V20-001) | Layer 1 + Layer 2 (V20-002/003/004) | Local logic + live Polar HTTP wrappers for refunds, license, organizations |
| EcommerceStoreManagement.Publisher | Layer 1 only | Local orchestration + idempotency. Polar HTTP returns stub `UnexpectedFailure` (V20-001) |
| Onboarding | Layer 1 only | Wizard + EF sink. Polar HTTP throws (V20-006) |
| Reporting (snapshot service + 12 advanced reports) | Layer 1 + Layer 2 (V20-005, 12 endpoint tests + idempotency) | Snapshot ingestion + report queries against live Polar |
| Translation providers (5 packages) | Layer 1 in `PolarSharp.EcommerceStoreManagement.Translation.Tests` | Provider behavior — but per-provider live HTTP tests are NOT present |
| PrepaidWallets (Phase 20) | Layer 1 + Layer 4 (EF + Marten event-stores) | Wallet domain + FIFO bucket allocation + event persistence. **No Polar interop tests** — bridges are scaffolds |
| Storefronts Cart/Checkout/Customer + GuestSessions (Phase 25) | Layer 1 only | Cart fraud-prevention + checkout snapshot + guest session middleware. **Pipeline downstream is stubs (Phase 26)** |
| Storefronts Polar bridges (9 packages), Pipelines (3 packages), Themes/SEO/Search/Shipping/Tax/WebComponents | None | Scaffolds; `IsPackable=false` |
| CustomerGraph, NaturalLanguageQuery, AuditLog.Marten, Onboarding.Wizard.Marten, Reporting.Marten | None | Scaffolds |

---

## When you add a new test

Use this decision tree:

```
Is the test hitting Polar.sh's HTTP API?
├── Yes — uses real sandbox endpoint
│      → put in the relevant package's *.Tests project
│      → tag the test class with [Trait("Category", "Integration")]
│      → guard every test method with `if (string.IsNullOrEmpty(Token)) return;`
│      → mirror the pattern from PolarClientRefundsApiIntegrationTests.cs
│      → ⚠ TASK-V14-006: also add the project to the CI Integration job's `dotnet test` command, or it won't actually run in CI
└── No — uses fakes, in-memory, or WebApplicationFactory
       → do NOT tag with Category=Integration
       → put in the relevant package's *.Tests project
       → it'll run automatically under CI's "Build, Test & AOT" step
```

When the test needs a real database (not SQLite-in-memory) → use Testcontainers (Layer 4). When the test needs the Cosmos emulator → use `[SkippableFact]` + `Skip.IfNot(...)` so machines without the emulator stay green.
