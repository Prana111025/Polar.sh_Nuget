# Large Project Best Practices for PolarSharp

**Audience:** future contributors + agentic AI agents working on PolarSharp. Read this BEFORE starting any non-trivial planning, design, or implementation session.

**Purpose:** Codify the methodology developed during the 2026-05-19 architecture session that produced the v1.4.0 EcommerceStorefronts WebComponents design (~80 individual design decisions, ~85 WCs catalogued, ~25 new backend entities scoped, the audit-fix sweep, and the PolarSharp.EcommerceStorefronts.Abstractions static-class registries). This document captures the patterns that worked, the mistakes made + corrections, and the reasoning behind every decision — so the methodology is reproducible by both humans and agents without re-discovering the same lessons.

**Outcome target:** future sessions can pick up cold, read this + CLAUDE.md + PLAN.md + memory notes + the task list, and proceed with the same discipline that produced ~440% PLAN.md growth in one session WITHOUT introducing new regressions, accidental scope drift, or the audit-found trap patterns.

---

## Quick-reference card (1-page TL;DR)

### Top 10 patterns

1. **Audit BEFORE building** — dispatch a focused audit subagent when worried about stubs/placeholders or before a major feature wave
2. **Stub safely (entities + interfaces); never stub services with NotImplementedException** — see Trap Catalog #1 + #2
3. **5-layer tenant isolation for every new entity** — ITenantOwned + RLS + SQLite-per-file + Cosmos partition + MariaDB filter + single-tenant mode
4. **Per-tenant configurability as agent-driven default** — when imagining 2+ marketplaces wanting different values, lean configurable
5. **Code-as-source-of-truth for agent consumption** — static-class registries (e.g. `PolarThemeTokens`, `WcCatalog`) beat prose
6. **Verify before claiming** — WebSearch for framework capabilities; Read for codebase state
7. **Memory + tasks + PLAN.md + this white paper as four persistence layers** — match decision to layer by lifetime
8. **AskUserQuestion batches of 1-4 with "Recommended" markers + explicit tradeoffs** — let user redirect specifically
9. **Per-question clarification protocol** — when user wants to clarify, list concrete possibilities; don't blindly reformulate
10. **Subagent dispatches for substantial mechanical work** — preserve main conversation context for decisions

### Top 10 anti-patterns (Trap Catalog summary)

1. **DI-injected interface without default impl** → runtime crash trap (audit-found #66)
2. **NotImplementedException stubs for concrete services** → silent failure trap
3. **Claiming framework capabilities without verification** → stale-training-cutoff trap (passkey mistake)
4. **Listing new entities without 5-layer isolation criteria** → cross-tenant data leak trap
5. **Burying permanent decisions in task descriptions** → lost-on-task-close trap
6. **Tunnel-vision past user redirects** → wasted-batch trap
7. **PLAN.md prose drifting from codebase reality** → stale-claim trap (`AuditLogSaveChangesInterceptor` was real but PLAN.md said "comment only")
8. **Assuming per-tenant configurability is "nice to have"** → marketplace-rigidity trap
9. **Shipping packages at v1.0.0 with zero source** → empty-package trap (audit-found 30-package issue)
10. **Auto-deferring features when user offers "willing to defer"** → missed-reframe trap (accessibility-first reframe was the right call)

### Critical cold-start artifacts (read before any non-trivial work)

**Project-rooted (in `/Users/mollsandhersh/Repos/Polar.sh_Nuget/`):**

| Artifact | Why |
|---|---|
| `CLAUDE.md` | Project standing requirements (build commands, doc standards, framing rules) |
| `LARGE-PROJECT-BEST-PRACTICES.md` (this doc) | Methodology |
| `PLAN.md` | Current technical plan (active sprint + upcoming sprints + future strategy) |
| `TASKS.md` | Current tasks |
| `PROGRESS.md` | Completed work log |
| `DECISIONS.md` | Locked architectural decisions |
| `Case Studies/*.md` | Five canonical architectural patterns |

**Home-rooted (in `/Users/mollsandhersh/`; universal across all Claude projects):**

| Artifact | Why |
|---|---|
| `CLAUDE.md` | Home standing requirements (agentic-master policy, RAG policy) |
| `AGENTS.md` | Cross-project workflow rules + agentic-master policy + RAG policy |
| `ZoranHorvat.md` | Cross-project .NET / C# / Blazor / Telerik / ServiceStack coding standards |

**Per-project (in `.claude/projects/{project-hash}/memory/`):**

| Artifact | Why |
|---|---|
| `MEMORY.md` | Memory index → individual behavioral/project memory notes |

**Location migration note (2026-05-19):** PLAN.md / TASKS.md / PROGRESS.md / DECISIONS.md were previously located at home-directory paths (e.g. `/Users/mollsandhersh/PLAN.md`); they were moved into the project folder so multiple Claude projects on the same machine don't collide on shared planning files. Memory notes + this doc + project CLAUDE.md were all updated for this layout. Older sessions / external references that still expect the home-directory paths will need to be updated.

---

## The three-layer persistence model

Different artifacts have different lifetimes + audiences. Use each for what it's good at; don't conflate them.

| Layer | Lifetime | Audience | What goes here |
|---|---|---|---|
| **Memory notes** (`.claude/projects/{project}/memory/`) | Cross-session, indefinite | Future me / future agent sessions | Behavioral rules, architectural anchors, user-feedback corrections, framework-capability discoveries, lessons that shouldn't be re-learned. Examples: "verify .NET capabilities via WebSearch before claiming missing", "every new entity needs 5-layer tenant isolation", "PolarSharp doesn't talk to Stripe ever". |
| **Task list** (`TaskCreate` / `TaskUpdate`) | In-session + can survive across | Current + next session | Discrete work items being tracked (in-flight + pending + completed). Use it as the inventory of "what needs to happen next" — both implementation TODOs and design decisions in flight. |
| **PLAN.md / CLAUDE.md / docs/** | Committed to repo; permanent | Everyone reading the project | Canonical design narrative, standing requirements, architectural decisions with rationale. Long-form prose + tables + diagrams + cross-references. |

**Discipline:** when you make a decision, ask "what's its lifetime + audience?" and write it to the right layer. Don't bury a permanent architectural commitment in a task description; don't try to capture every small WIP item in PLAN.md.

---

## The core methodology: design → stub → implement → test → iterate

### Phase 1: Design (in this conversation)

- Use AskUserQuestion batches of 1-4 questions with clear options + "Recommended" markers + explicit tradeoffs. Lets the user redirect specifically without prose ambiguity.
- When the user rejects a question with "wants to clarify", ASK them what specifically they want clarified (provide concrete possibilities; don't blindly reformulate). Pattern: "Possibilities of what might be unclear — let me know which matches: [list of 5-7 specific candidates]."
- Capture decisions in the right persistence layer as you go. Lock decisions in memory notes IF they're behavioral rules; in task descriptions IF they're work items; in PLAN.md IF they're canonical design.
- When the user redirects mid-walkthrough ("we need to also support SSO"), treat it as signal: pause the current batch, address the new concern, integrate it into the design, return to the walkthrough. Don't tunnel-vision past redirects.

### Phase 2: Stub the contracts (safely)

After design lands, stub the pure-contract artifacts in code WHILE MEMORY IS FRESH. This is the highest-leverage drift-prevention activity. Three categories:

| Category | Safe to stub | Notes |
|---|---|---|
| **Entity records** (immutable; pure data) | ✓ ALWAYS | No DI implication. Just locks the data shape. |
| **Enums + discriminated unions** | ✓ ALWAYS | Pure values. |
| **Options classes + validators** | ✓ ALWAYS | Pure contracts. |
| **Static-class registries** (e.g. `PolarThemeTokens`, `WcCatalog`) | ✓ ALWAYS | Agent-consumable canonical metadata. Highest value for agent-driven workflows. |
| **Interface definitions (Flavor A — NOT DI-registered)** | ✓ ALWAYS | Pure contract. Host implements; PolarSharp consumes. No runtime trap risk. |
| **Interface definitions (Flavor B — DI-registered)** | ✓ ONLY WITH `StubXxx` FAIL-LOUD DEFAULT | If you define a DI-injected interface without a default impl, DI resolution crashes at runtime. Audit-found trap (task #66). Always ship a `Stub{Interface}` concrete impl that throws `NotSupportedException` with a clear "TASK-V20-NNN deferred" message, registered via `TryAddScoped` so hosts can override. |
| **Concrete services with method bodies** | ✗ NEVER stub with `NotImplementedException` | Creates the audit-found trap pattern. Either implement fully OR ship a fail-loud Stub default + register via TryAdd. |
| **WC implementations** (Stencil JS) | ✗ NEVER stub | Real UI; partial stubs render broken WCs that look-real-but-aren't. |
| **EF migrations** | ✗ NEVER stub | Sequenced + ordered state; stubs create wrong migration history. |

**Canonical pattern for Flavor B interface stubs:** `src/PolarSharp.Onboarding/StubKiotaPolarOnboardingApi.cs` (committed in `5354e89`). Every new Flavor B interface should mirror this:

1. Implement the interface as `internal sealed class Stub{Interface}`
2. Inject `ILogger<>`
3. Every method throws `NotSupportedException` with: which method was called + which TASK-V20-NNN tracks the deferral + how the host can supply their own impl
4. Log an `Error`-level message before throwing (gives audit trail when the stub is hit)
5. Register via `services.TryAddScoped<TInterface, StubTInterface>()` in the package's `AddPolarXxx()` extension
6. Add regression tests confirming DI resolution succeeds + first call throws with the expected message

### Phase 3: Implement (focused sessions; usually agent-dispatched)

- For substantial mechanical implementation work, dispatch a subagent with a comprehensive brief. Main conversation context stays focused on decisions; agent context handles transcription/wiring.
- Subagent brief MUST include: what to read first (PLAN.md sections, memory notes, existing patterns to mirror), deliverables (files + tests + verification commands), constraints (AOT-compat, CS1591, lift-safe, isolation requirements), report-back format with size limit.
- Trust-but-verify: when subagent reports done, spot-check the actual changes before claiming success.

### Phase 4: Test (cross-tenant isolation regressions are non-negotiable)

- Every new tenant-scoped entity gets a cross-tenant isolation regression test per the existing `CrossTenantIsolationTests` template.
- Every new Flavor B interface gets a regression test confirming DI resolution + first-call fail-loud message.
- Every new public type/member gets XML doc comments (CS1591 is build error).
- Schema snapshot tests for GraphQL schemas (Verify-based) + Public API snapshot tests for breaking-change detection.
- Live Polar sandbox tests for every new `IPolarXxxApi` wrapper (per memory note `feedback_live_polar_tests_required.md`).

### Phase 5: Iterate (PLAN.md chunk-by-chunk updates)

- For substantial design landings, update PLAN.md in chunks (5-8 chunks rather than one massive drop). Per-chunk user review allows mid-stream redirects + tighter quality.
- Restructure first (e.g. promote a sub-section to top-level) BEFORE filling in content.
- End each chunk with a pointer to remaining chunks so the user can see progress.
- After all chunks land, remove the pointer + close the section cleanly.

---

## The audit-before-building pattern

When the user is concerned about "stubs/placeholders that may have been overlooked" — OR when planning a major new feature wave — dispatch a thorough audit subagent FIRST. This is a high-leverage activity that surfaces critical findings cheaply.

**Audit briefing template:**

1. Read the entire src/ tree for: `NotImplementedException`/`NotSupportedException` throws; TODO/FIXME/XXX/HACK comments; stub/placeholder/not-implemented mentions; suspicious empty bodies (`return null`, `return default`, `Task.CompletedTask`); skipped tests (`Skip = "..."`); interfaces without implementations; DI registrations without consumers; options classes without validators; IHostedService classes that no-op.
2. Classify findings into three categories:
   - **Category A — Intentional deferral per plan** (don't flag as bugs)
   - **Category B — Probably intentional, worth confirming**
   - **Category C — SUSPICIOUS, may be inadvertent stubs**
3. Severity-rank Category C; flag CRITICAL findings with `**CRITICAL**`.
4. Report what was NOT audited (scope honesty).

**Audit-found pattern: the DI-injected-interface-without-default trap.** The 2026-05-19 audit found `IPolarOnboardingApi` had no concrete implementation in `src/`; `PolarOnboardingClient` required it via DI; resolution crashed at runtime; the README implied it worked. Fixed in task #66 with `StubKiotaPolarOnboardingApi` per the canonical pattern above. **NEVER create another instance of this trap.** Every DI-injected interface MUST have a default impl (real OR fail-loud Stub) registered via TryAdd.

---

## The five-layer tenant-isolation acceptance criterion (NON-NEGOTIABLE)

Every new entity / table / column introduced by ANY feature must satisfy all five layers:

| Layer | Requirement | Where enforced |
|---|---|---|
| **L1 — App-layer query filter** | Entity implements `ITenantOwned` (carries `TenantId`); `TenantAwareDbContextBase` auto-applies global query filter | EF Core in `PolarSharp.MultiTenant.EntityFrameworkCore` |
| **L2 — Database RLS** (SqlServer + Postgres) | EF migration creates RLS policy on the new table | Initial EF migration per SqlServer/Postgres provider |
| **L0 — SQLite per-tenant `.db` file** | Entity sits on the appropriate DbContext; existing `SqlitePerTenantDbContextFactory` routes | Existing SQLite provider plumbing |
| **MariaDB** | App-layer filter only (no native RLS); documented as posture-difference | Existing MariaDb provider |
| **Cosmos DB** | `TenantId` is `/tenantId` partition key | Existing Cosmos provider |

Plus: **single-tenant mode behavior** — when `IsMultiTenantMode = false`, the filter is a no-op but the entity keeps its `TenantId` column for schema consistency. SaaS-as-itself deployments use a single fixed tenant id.

**Cross-tenant isolation regression tests are required for every new entity.** Template: `tests/PolarSharp.MultiTenant.EntityFrameworkCore.Tests/CrossTenantIsolationTests.cs`.

Memory note (existing): `feedback_tenant_isolation_every_new_entity.md`.

---

## Per-tenant configurability as the agent-driven default

When designing a new feature/setting, default to **per-tenant configurable** if you can imagine 2+ marketplaces wanting different values. The army-of-marketplaces deployment model means each tenant is constructed by an agent for a different niche; per-marketplace tuning beats platform-wide rigid defaults.

Pattern observed during the auth-wizard design walkthrough: **8 of 8 auth-wizard decisions tilted to per-tenant configurable** (or platform-default + per-tenant override). When you see this pattern emerging, lean into it — surface "per-tenant configurable" as an option in your AskUserQuestion batches.

This implies a recurring per-tenant config entity (e.g. `TenantSignupConfig` carrying the 8 auth-wizard knobs; `TenantSsoProvider` per provider; `TenantAccessibilityPolicy` on `TenantBusinessProfile`). Each config knob persists per-tenant + agents read it at marketplace construction time + can be modified by the tenant admin without code change.

Memory note (existing): `project_agent_driven_marketplaces.md`.

---

## Code-as-source-of-truth for agent consumption

PLAN.md is the human-readable design narrative. Agents need machine-parseable contracts they can mechanically filter on.

**Pattern:** for any catalog of entities/features/options that agents consume, lock it as code (static-class registries + records + enums) rather than only prose. Example from the 2026-05-19 session:

- `PolarThemeTokens` static class enumerates the 31 design tokens with type-safe descriptors → agent reads `PolarThemeTokens.AllTokens` for the canonical list + per-token metadata. No risk of agent misreading PLAN.md prose.
- `WcCatalog` static class enumerates the 87 WCs with their full frontmatter metadata (purpose, data-source, composes-with, conflicts-with, audience, deployment, ship-target, decision-tree-tags, accessibility) → agent reads `WcCatalog.AllWcs` + filters by decision-tree-tags for the marketplace's niche profile.

**Compile-checked vs prose-checked:** the code-as-source-of-truth registry is enforced by the compiler — typos can't slip past. PLAN.md prose can drift silently. Lock the contract in code first; refer to it from PLAN.md prose; never the reverse.

**Use when:**
- The catalog has > 10 entries (any larger, prose-only is hard to audit)
- Multiple downstream consumers need the metadata (agents, theme editor, validation tests, doc generators)
- The catalog WILL grow + change over time (compile-checked changes prevent silent drift)

---

## Verify before claiming (framework capabilities, codebase state)

Training cutoffs lie. Frameworks ship features faster than cutoffs update. **When you're about to claim "X isn't supported in framework Y" or "this code does Z", verify first.**

**Verify framework capabilities via WebSearch.** Specifically for .NET / ASP.NET Core: `learn.microsoft.com/.../view=aspnetcore-{version}` URLs are pinned to specific framework versions; trust those over training-cutoff knowledge.

**Lesson from 2026-05-19:** I claimed ASP.NET Core Identity in .NET 10 didn't have native passkey support, recommended a Fido2.NetFramework integration as a 1-2 week project deferred to v1.4.x. User correctly challenged. WebSearch found Microsoft Learn passkey docs explicitly tagged for .NET 10. Work scope shrank from 1-2 weeks to 1-2 days; passkey WCs moved from v1.4.x to v1.4.0.

Memory note (existing): `feedback_verify_dotnet_capabilities.md`.

**Verify codebase state by reading the actual files.** When PLAN.md / TASKS.md / PROGRESS.md make a claim ("this isn't implemented yet"), check the actual code before propagating the claim. The audit found `AuditLogSaveChangesInterceptor` was listed as "currently only the comment exists" in PLAN.md, but the file was actually a 225-line real implementation wired into all 5 providers. Trust the code over stale planning prose.

---

## Lift-and-shift boundary discipline (the `.Polar.*` namespace separator)

When designing a subsystem that COULD one day be extracted to its own repo (wallets, storefronts, future MediaAndFileStorage, future affiliate system), follow the established lift-and-shift pattern from day one. Don't build it as PolarSharp-coupled "we'll extract later" code.

**Pattern:**

- **Lift-safe core packages** (`PolarSharp.{Family}.*`, no `.Polar.` infix) have ZERO `PolarSharp.*` dependencies. Pure abstractions + entities + value types + provider implementations that don't know they're inside PolarSharp.
- **Polar bridges** (`PolarSharp.{Family}.Polar.*`) wire the lift-safe core into PolarSharp's identity/notifications/reporting/etc. surfaces. Polar bridges depend on PolarSharp.*.
- **CI guard** verifies the lift-safe core has zero `PolarSharp.*` dependencies — the namespace separator is the entire lift contract.

**Why this matters:** future agent-driven feature work often wants to package + redistribute subsystems. Building lift-safe from day one means extraction is mechanical (rename + repackage), not a multi-month refactor.

Reference: PrepaidWallets (v1.3 reference implementation), EcommerceStorefronts (v1.4 reference implementation).

---

## Per-question clarification protocol

When user rejects an AskUserQuestion with "wants to clarify": DON'T blindly reformulate. ASK them what specifically they want clarified, with concrete possibilities.

**Template:**

```
What would you like to clarify? Possibilities of what might be unclear — let me know which (or describe it):

- [Concrete possibility 1 with 1-sentence framing]
- [Concrete possibility 2]
- [Concrete possibility 3]
- [Possibility about scope]
- [Possibility about UX]
- [Possibility about implementation cost]
- Something else entirely (describe it)
```

This pattern came out of multiple iterations during the 2026-05-19 design walkthrough. User got faster + tighter responses + we stopped going in circles. Without this protocol, "wants to clarify" devolves into endless reformulation.

---

## Subagent dispatch protocol for substantial work

When work is mechanical (transcription, structured implementation, audit, codebase scan) and would consume substantial main-conversation context, dispatch a subagent. Main conversation stays focused on decisions.

**Subagent brief MUST include:**

1. **Why this matters** — strategic context (1-2 paragraphs); helps subagent make judgment calls aligned with the project's goals.
2. **Read these first (in order)** — specific files + memory notes + existing patterns to mirror. The subagent has no conversation context; it needs to bootstrap from artifacts.
3. **Deliverables** — concrete file names + types + locations. Don't make them guess what shape you want.
4. **Constraints + standards** — AOT-compat, CS1591, lift-safe, isolation requirements, tenant-AI policy compliance, etc.
5. **Report back format** — bounded length (≤ 500 words usually). Specify what you want in the report (files modified, build status, test count, judgment calls, gaps).

**After subagent reports:**

- Trust-but-verify. Spot-check actual file changes. Especially if they made judgment calls or noted gaps.
- Subagent often surfaces inconsistencies you didn't notice (e.g. the chunk-7 subagent caught the 85-vs-87 WC count discrepancy in PLAN.md). Take these seriously.
- If subagent's work touches files concurrent with your main-conversation work, partition staging carefully so commits don't accidentally bundle.

---

## Mistakes made + corrections (case studies)

Specific lessons from the 2026-05-19 session, captured so we don't repeat them.

### Mistake 1: Claimed .NET 10 didn't have native passkey support

**What happened:** Recommended a 1-2 week Fido2.NetFramework integration for passkey WCs, deferred to v1.4.x.

**User correction:** "Double check on this since I thought it was in fact already available and supported."

**Fix:** Loaded WebSearch tool, queried Microsoft Learn, confirmed `view=aspnetcore-10.0` docs show native passkey support is shipped. Updated task #80 from "Fido2 library integration v1.4.x" to "wire .NET 10 native passkey APIs v1.4.0" (~1-2 days, not 1-2 weeks). Saved memory `feedback_verify_dotnet_capabilities.md`.

**Lesson:** WebSearch before claiming "X isn't in framework Y". Especially when user pushes back — that's signal.

### Mistake 2: Listed entities without 5-layer isolation criteria

**What happened:** During WC backend additions discussion, listed new entities (ProductReview, CuratedCollection, ShoppableImage, LoyaltyTier, ReferralCode, PromoBanner, etc.) WITHOUT explicitly calling out their tenant-isolation treatment.

**User correction:** "I want to ensure that any features like this that involve database additions/modifications that the SQL queries and strictly guarded against cross-tenant data... this includes similar logic for multitenancy needs while using SQLite individual tenant databases. Lastly you must ensure that single tenant scenarios are fully supported."

**Fix:** Added inline isolation note to every subsequent WC backend addition. Updated task #77 PLAN.md acceptance criteria to require the isolation block. Saved memory `feedback_tenant_isolation_every_new_entity.md`.

**Lesson:** Tenant isolation is a non-negotiable acceptance criterion, not an assumed default. Spell it out for EVERY new entity proposal.

### Mistake 3: Framed accessibility as additive instead of accessibility-first

**What happened:** Initially proposed adding color-blindness simulator + WCAG contrast checker + accessible-palette suggestions as features TO ADD to the theme editor.

**User clarification:** "The accessibility additions, but I'm willing to defer if the bigger picture seems more prudent at this time."

**Fix:** Recognized the reframe opportunity. Articulated accessibility-first as the right framing (3 reasons: agent-driven defaults, easier to bake in than retrofit, "different + better than Shopify" differentiator). User picked the reframe. Updated tasks #74 + #89 to reflect strengthened design.

**Lesson:** When user offers "willing to defer if X seems prudent", consider whether X (the reframe) is actually better. Don't default-defer; push back if the reframe wins.

### Mistake 4: Undersold the reviews/AI-summary scope

**What happened:** Initially proposed `polar-product-questions` for product Q&A, but didn't separately scope ratings + reviews + AI-summarized pros/cons consensus.

**User question:** "Will this handle end-user ratings as well (ie: 1-5 star rating system) plus customers overall pros/cons consensus driven by aggregated AI summarizations?"

**Fix:** Recognized Q&A and Reviews are conceptually distinct (per Amazon's model). Proposed 4 new WCs (polar-product-rating, polar-product-reviews, polar-product-review-form, polar-product-ai-summary) + backend additions. All 4 + Q&A landed v1.4.0.

**Lesson:** When proposing a feature, think about adjacent features the user MIGHT also want. Surface them proactively; don't wait for the user to discover the gap.

### Mistake 5: AskUserQuestion with 5 options (max 4)

**What happened:** Tool validation error: `too_big: maximum 4`.

**Fix:** Consolidated two similar options into one.

**Lesson:** AskUserQuestion has 1-4 question count + 2-4 options-per-question. Memorize the limits; consolidate similar options.

### Mistake 6: Underestimated catalog walkthrough scope

**What happened:** Initial estimate "~50 questions across ~12-15 batched turns". Actual: ~80 individual decisions across 24+ batches.

**Lesson:** When the user picks "walk WC-by-WC like the token decisions", expect 50-100% MORE batches than you initially estimate. The user often adds new items mid-walkthrough that expand scope.

### Mistake 7: PLAN.md tally inconsistencies during chunk-7

**What happened:** Wrote "85 WCs" but categorical totals + deferred-to-planning = 87. Subagent caught + reconciled in `WcCatalog` registry.

**Lesson:** When transcribing prose → code, the compiler catches inconsistencies you didn't notice in prose. Trust the code-as-source-of-truth audit.

### Mistake 8 (2026-05-20): Treated passing test counts as proof of functionality

**What happened:** During a "what works vs what doesn't" audit, claimed the v1.3.0 service surface was largely "Tier A — fully functional, end-to-end works." Cited 1163/1163 tests passing as evidence. The user pushed back: "Do the unit tests, integration tests, and regression tests fully reflect/bare-out your findings?"

**What investigation surfaced:**
- ~16 live-Polar "integration" tests used `if (string.IsNullOrEmpty(Token)) return;` and reported as **Passed** when `POLAR_SANDBOX_TOKEN` was unset. Silent-skip looks identical to silent-pass at the test runner level.
- CI's "Integration Tests (sandbox)" job ran `dotnet test tests/PolarSharp.IntegrationTests --filter Category=Integration` — but that project has zero `Category=Integration` tests. CI logged "No test matches the given testcase filter" and exited green having run zero tests. The integration job had been a no-op for an extended period.
- TASKS.md still listed V20-002/003/004 as "Deferred → v2.0" while the code actually shipped + had live-sandbox tests; CHANGELOG was correct, TASKS.md was stale. The doc drift mislead me into thinking those features were stubs.
- ~17 packages had `<IsPackable>true</IsPackable>` (the default) but their bodies were no-op extension methods with XML doc explicitly saying "Phase X.x ships the registration scaffold; the full impl lands in Phase X.y." These would silently ship empty NuGet packages on any tag.

**Fix (2026-05-20 testing overhaul):**
- Converted silent-skip to `[SkippableFact]` + `Skip.If(...)` across 16 test classes — green now means actually-ran-and-passed; honest reporting is enforced.
- Fixed CI Integration job to target the two test projects that own the live tests (`EcommerceStoreManagement.Tests` + `Reporting.Tests`).
- Added `ScaffoldIntegrityTests` — a structural test that enumerates every `IsPackable=false` package and asserts ≤3 hand-written `.cs` files. When a contributor adds real code without dropping the IsPackable flag, this fails loudly.
- Marked 17 packable-but-scaffold packages as `IsPackable=false` so they can't ship empty.
- Closed TASKS.md V20-002/003/004 to match code reality.
- Wrote TESTING.md documenting the four test layers, the silent-skip gotcha, and which classes prove what.

**Lessons:**
1. **Passing tests prove what they assert; nothing more.** A test that early-returns on a missing env var asserts nothing. Count the tests that actually executed, not the tests that reported Passed.
2. **CI green doesn't mean CI ran.** Workflow filter syntax that matches zero tests exits successfully with a misleading message. Always verify the test count printed by the CI log matches the test count you expected.
3. **Audit YOUR audits.** When asked "what works?" the source-inspection answer can mislead. Running the actual tests with `dotnet test` + reading the test source + checking the project filters is the real audit. Documentation will drift; code + tests are ground truth.
4. **Doc drift is silent until tested.** TASKS.md said V20-002 was "Deferred." Code said it was shipped. Without cross-referencing both against the actual source, the drift compounds.
5. **The scaffold integrity invariant has to be tested, not just documented.** `<IsPackable>false</IsPackable>` is a comment to humans; only a test that walks the csproj files turns it into a checked contract.

---

## Trap pattern catalog (enumerated anti-patterns)

These are the recurring failure modes the methodology defends against. Recognize them on sight; redirect before they happen.

### Trap 1: DI-injected interface without default implementation

**Shape:** An interface is defined in `src/`; another PolarSharp service requires it via constructor injection; no concrete implementation is registered in DI.

**Failure mode:** Runtime `InvalidOperationException: Unable to resolve service for type 'X'` on first DI resolve. README implies it works; nothing in compile-time checks catches the gap.

**Detection:** Audit subagent greps for `interface I*` then checks whether any `services.AddXxx<>` registers a concrete impl. Audit found `IPolarOnboardingApi` this way (task #66).

**Avoidance:** Every DI-injected interface MUST ship a fail-loud `Stub{Interface}` default registered via `TryAddScoped<TInterface, StubTInterface>()`. Mirror `StubKiotaPolarOnboardingApi` (commit `5354e89`).

### Trap 2: NotImplementedException stubs for concrete services

**Shape:** A concrete service class implements an interface; all methods throw `NotImplementedException`; DI registration says "I'm shipping a real implementation".

**Failure mode:** Customer-facing surface looks complete; first call throws on the host. No fail-loud warning that this is a deferred TODO.

**Detection:** Audit subagent greps for `throw new NotImplementedException()` then checks whether the surrounding code is documented as a planning stub.

**Avoidance:** For services in pre-implementation state, ship the fail-loud `StubXxx` pattern (clear error message naming the deferred TASK + how to override). OR mark the package `IsPackable=false` until implementation lands (audit-found 30-package fix #68).

### Trap 3: Claiming framework capabilities without verification

**Shape:** Claiming "X isn't in framework Y" or "framework version Z doesn't support feature A" from training-cutoff knowledge.

**Failure mode:** Wasted design time + over-scoped task estimates + missed framework features. Audit-found instance: claimed .NET 10 ASP.NET Core Identity didn't have native passkey support; recommended a 1-2 week Fido2 integration. User correctly challenged. WebSearch confirmed native passkey support shipped in .NET 10. Work scope shrank from 1-2 weeks to 1-2 days.

**Detection:** Self-detection — pause whenever you're about to make a capability claim about a framework. User-detection — when user pushes back ("are you sure?"), take it as signal to verify.

**Avoidance:** WebSearch the `learn.microsoft.com/.../view=aspnetcore-{version}` docs (or equivalent for other frameworks). Memory note `feedback_verify_dotnet_capabilities.md`.

### Trap 4: New entity without 5-layer isolation criteria

**Shape:** Proposing a new entity (`ProductReview`, `LoyaltyTier`, `CuratedCollection`, etc.) without explicitly calling out: ITenantOwned + RLS + SQLite-per-file + Cosmos partition + MariaDB filter + single-tenant mode.

**Failure mode:** Cross-tenant data leak. Implementer reads the proposal, builds the entity, forgets one of the layers; tenant A's data appears in tenant B's queries.

**Detection:** Self-detection — when proposing a new entity, the 6-line isolation block should be inline in the proposal. If absent, the proposal is incomplete.

**Avoidance:** Always inline the isolation block. Memory note `feedback_tenant_isolation_every_new_entity.md`. Cross-tenant regression test per the `CrossTenantIsolationTests` template.

### Trap 5: Burying permanent decisions in task descriptions

**Shape:** A permanent architectural decision ("we will use 8-character minimum passwords; the tenant-AI policy never falls back to SaaS-master") gets captured only in a task description that will eventually close.

**Failure mode:** Task closes; decision is lost; future contributors re-derive it (possibly differently); inconsistency creeps in.

**Detection:** When making a decision, ask "is this a permanent rule or a work item?" If permanent → memory note OR PLAN.md, not task list.

**Avoidance:** Match decision lifetime to persistence layer per the three-layer persistence model. See Tool Decision Trees below.

### Trap 6: Tunnel-vision past user redirects

**Shape:** Mid-walkthrough, user says "we need to also support SSO providers" or "wait, what about color-blindness?" — pressing forward with the current batch instead of integrating the redirect.

**Failure mode:** Redirected concern gets lost; design ends up inconsistent (e.g. auth-wizard designed without SSO consideration; theme editor designed without accessibility consideration).

**Detection:** Recognize user redirects as signal, not interruption. They're course-correcting before drift gets worse.

**Avoidance:** Pause the current batch. Address the redirect (design + capture). Then return to the walkthrough with the redirect integrated.

### Trap 7: PLAN.md prose drifting from codebase reality

**Shape:** PLAN.md / TASKS.md / PROGRESS.md make a claim ("X isn't implemented"); reality differs. Future readers trust the prose; build on the false claim.

**Failure mode:** Audit-found instance: PLAN.md claimed `AuditLogSaveChangesInterceptor` was "currently only the comment exists"; reality was a 225-line real impl wired into all 5 providers. Future implementers would have re-implemented it.

**Detection:** When a planning artifact makes a claim about codebase state, check the codebase before propagating.

**Avoidance:** Trust the code over stale planning prose. Update the planning prose when reality changes. Audit-fix #71 corrected this for V20-013.

### Trap 8: Assuming per-tenant configurability is "nice to have"

**Shape:** Designing a setting/feature with a hard-coded default; not exposing per-tenant override.

**Failure mode:** Marketplace operators with different niches (fashion vs electronics; B2B vs B2C; regulated vs casual) can't tune for their context. Agent-driven construction loses flexibility — agent can't differentiate marketplace #57 from #58 on this dimension.

**Detection:** When proposing a new setting, ask "could I imagine 2 different marketplaces wanting different values?" If yes → per-tenant configurable.

**Avoidance:** Lean per-tenant configurable by default; only hardcode when it's truly invariant (e.g. password breach-check is on for all tenants; no tenant can disable security floor).

### Trap 9: Shipping packages at v1.0.0 with zero source

**Shape:** Empty NuGet packages get published at `<Version>1.0.0</Version>` with `<NoWarn>CS1591</NoWarn>` to suppress missing-docs warnings. Consumer installs; gets an empty assembly with no build-time signal.

**Failure mode:** Tenant `dotnet add package PolarSharp.X`; gets nothing. The README's "Status: scaffold" note is the only warning.

**Detection:** Audit-found pattern (30 empty `PolarSharp.EcommerceStorefronts.*` packages).

**Avoidance:** Mark `<IsPackable>false</IsPackable>` until real source lands. Flip back when implementation ships. Audit-fix #68 enforced this on the 30 packages.

### Trap 10: Auto-deferring features when user offers "willing to defer"

**Shape:** User says "I'm willing to defer if the bigger picture seems more prudent." Default response: auto-defer.

**Failure mode:** Missed reframe opportunity. Audit-found instance: user offered to defer accessibility additions; I considered whether the reframe (accessibility-first instead of accessibility-additive) was actually better; recognized it was; reframed. Auto-deferring would have left accessibility as a v1.4.x patch instead of v1.4.0 baseline.

**Detection:** When user offers to defer, ask: "is the reframe actually better than the current direction?"

**Avoidance:** Consider the reframe seriously. Push back on auto-defer when the reframe wins. User-redirects-are-signal applies to "willing to defer" prompts too.

### Trap 11: Silent-skip in env-gated tests reads as Passed (2026-05-20)

**Shape:** A test that needs a credential (sandbox token, API key, connection string) is gated with `if (string.IsNullOrEmpty(Token)) return;`. Without the credential the method returns without asserting; xUnit reports it as Passed.

**Failure mode:** A green test count is meaningless. On machines without the credential, the audit-relevant tests are NOT running, but `dotnet test` returns "Passed!" anyway. Code review of test code looks correct; the trap is in the runner's interpretation of a no-op method.

**Detection:** Grep for `if (string.IsNullOrEmpty(*token*)) return` and `Environment.GetEnvironmentVariable(...) == null) return`. Each occurrence is a silent-skip.

**Avoidance:** Use `[SkippableFact]` + `Skip.If(condition, reason)` from `Xunit.SkippableFact`. The test reports as Skipped (not Passed) when the gate trips, so the runner output honestly reflects what executed. Audit-fix 2026-05-20 converted 32 occurrences across 16 files.

### Trap 12: CI workflow filter pointing at the wrong test project (2026-05-20)

**Shape:** A workflow step runs `dotnet test path/to/Project --filter "Category=X"` against a project that has zero `Category=X` tagged tests. The step exits successfully having run zero tests and logs the misleading message `No test matches the given testcase filter`.

**Failure mode:** CI reports green on a job that's testing nothing. Audit-found instance: `tests/PolarSharp.IntegrationTests` had zero `Category=Integration` tagged tests; the 16+ actual integration tests lived in two other projects. The CI Integration step had been a no-op for many weeks.

**Detection:** After every CI run that includes a filtered test step, count the tests printed in the runner output. Zero is a smell; investigate before merging anything that depends on the step's pass/fail.

**Avoidance:** Run the same filter locally before committing the workflow change. Or assert non-zero test count in the workflow itself (e.g., pipe through `tee` + `grep "Passed:" log | grep -v "Passed:     0"`).

### Trap 13: Source inspection lies about runtime behavior (2026-05-20)

**Shape:** Auditing functional state by reading source code without running it. A `PolarClientXxxApi` class with a "TASK-V20-XXX: deferred" marker in a stub method might be the OLD stub; the real one could exist in a different file. A README claiming "real impl in v1.4.0" might be aspirational doc that contradicts the actual code. The audit conclusions are wrong in either direction.

**Failure mode:** Audit reports "Tier A: fully functional" for code that actually throws on call (overclaim), OR "Tier B: stubbed" for code that's actually wired (underclaim). Either way the project owner gets a misleading picture.

**Detection:** When the audit conclusion is consequential (release readiness, "what works"), validate it by running the tests against the live external surface, not just by reading the source.

**Avoidance:** For "what works" audits, run `dotnet test` with the relevant credentials present. Diff the runner output against the audit-claimed state. Any discrepancy is a bug in the audit. Mistake #8 captures this exact failure mode.

---

## Tool / skill / artifact decision trees

When to use what. Codifies decisions that were intuitive during the 2026-05-19 session.

### Persistence layer decision

```
Is the decision permanent (cross-session)?
├─ No → Will it survive a task close?
│       ├─ No (in-flight WIP) → Plan in TaskCreate body OR conversation context
│       └─ Yes (tracking TODO) → Task list
└─ Yes → Is it a behavioral rule (do/don't pattern)?
        ├─ Yes → Memory note (feedback_*.md or project_*.md)
        └─ No → Is it canonical project design?
                ├─ Yes → PLAN.md (active) or DECISIONS.md (locked)
                └─ No (it's methodology) → LARGE-PROJECT-BEST-PRACTICES.md
```

### Subagent dispatch vs direct work decision

```
Is the work mechanical (transcription, audit, structured implementation)?
├─ Yes → Will it consume substantial main-conversation context?
│       ├─ Yes → Dispatch subagent with comprehensive brief
│       └─ No (small file edit, single grep) → Direct
└─ No → Is it a decision-making conversation?
        ├─ Yes → Direct (decisions need user iteration)
        └─ No (requires deep investigation across many files) → Dispatch with read-only scope
```

### Information-source decision

```
Need to know something about the codebase?
├─ Where is X defined / referenced? → Bash grep OR Explore subagent
├─ What does file Y currently contain? → Read tool
├─ What does the framework support? → WebSearch (especially for .NET / ASP.NET Core capabilities)
├─ What did we decide in a prior session? → Memory notes
└─ What's the canonical design? → PLAN.md / DECISIONS.md
```

### AskUserQuestion vs prose-question decision

```
Need user input?
├─ Multiple discrete options where I have a recommendation? → AskUserQuestion
├─ Open-ended ("what's your read on X?") → Plain prose question
├─ Many candidates and user might want to clarify? → Plain prose with concrete possibilities list
└─ Yes/no + I have strong recommendation? → State recommendation + ask "push back if disagree"
```

### Commit vs no-commit decision

```
Working tree is dirty. Should I commit?
├─ User explicitly said to commit? → Yes
├─ Task is complete + tests pass + builds clean? → Ask user before committing
├─ Audit-found bug fixed? → Bundle with descriptive commit message; ask user
├─ Mid-iteration, not ready for review? → No; leave dirty for user to review
└─ Subagent left dirty per "no-commit" convention? → Inspect, then ask user
```

---

## Agentic-AI-specific patterns

The army-of-marketplaces deployment model means agentic AI will run sessions, construct marketplaces, dispatch sub-agents, and consume the catalog metadata. Patterns specifically for that audience.

### Subagent briefing template

Every subagent dispatch should include:

1. **Why this matters** (strategic context; 1-2 paragraphs) — what outcome is this serving, why now, what does success look like
2. **Read these first (in order)** — specific files with line ranges where applicable, memory notes whose descriptions match, existing patterns to mirror (pointer to canonical example)
3. **Deliverables** — concrete file names + types + locations; don't make subagent guess shape
4. **Constraints + standards** — AOT-compat, CS1591, lift-safe, isolation requirements, anything from CLAUDE.md / this white paper
5. **Report-back format** — bounded length (≤ 500 words usually); specify: files modified, build status, test counts, judgment calls, gaps, working-tree state

### Trust-but-verify pattern after subagent reports

Subagent reports completion. Default to NOT immediately trusting:

1. **Read the actual changes** — git diff or read the new files. Don't trust "I implemented X" without seeing X.
2. **Check the build status independently** — run `dotnet build` or `dotnet test` yourself if any doubt.
3. **Verify judgment calls** — if subagent says "I inferred default values for accessibility metadata", spot-check whether the inferences match PLAN.md.
4. **Look for surfaced inconsistencies** — subagents often catch things you missed in prose (the 85-vs-87 WC count discrepancy). Take these seriously; don't dismiss.

### Context-budget management

Long sessions accumulate context. Strategies:

1. **Subagent dispatch** — primary lever. Substantial mechanical work delegated; main conversation preserves decision context.
2. **Status-update tables** — periodic "where we are at" summaries flush ephemeral details from main context.
3. **Memory notes as context-eviction-safe** — important architectural decisions written to memory survive main-context compaction.
4. **Avoid re-reading large files when grep would suffice** — `grep -rn "X"` returns hits + line numbers without flooding context with full file contents.

### Partial-subagent-failure handling

Subagent reports "partially completed" or "failed at step N":

1. **Verify what DID land** — read the changes that exist. May be more or less than reported.
2. **Identify the failure root cause** — was it a build error, a test failure, a misunderstanding of the brief?
3. **Decide: continue from where it failed, OR restart cleanly** — if partial work is correct, continue. If wrong direction, revert + restart.
4. **Update the brief based on what went wrong** — if subagent misunderstood a constraint, make it more explicit for the next dispatch.

### Agent-consumable artifact patterns

For artifacts that future agents will read:

1. **Static-class registries over prose enumerations** — `PolarThemeTokens`, `WcCatalog` are agent-grep-friendly + compile-checked.
2. **Machine-parseable frontmatter on docs** — YAML headers with structured fields (purpose, deployment, audience, ship-target) let agents filter mechanically.
3. **Cross-references to ground truth** — never just describe; link to the code/file/task/memory note that's authoritative.
4. **Explicit ship-target tags** — `v1.4.0` / `v1.4.x` / `v1.5+` lets agents know what's actually available when constructing a marketplace.
5. **Decision-tree tags** — `physical-goods`, `subscriptions`, `b2c`, `b2b`, `high-volume` let agents filter the catalog per the marketplace's niche profile.

### Communication conventions (codified)

Patterns reinforced by user feedback during the 2026-05-19 session:

- **"Recommended" markers** on AskUserQuestion options where I have a clear lean
- **Tradeoffs explicit in every option** — "this gets X but loses Y"
- **"I lean X because Y; you might prefer Z if your model is W"** — give the recommendation + the reasoning + the alternative + when to choose it
- **Per-question clarification with concrete possibilities** — list specific candidates rather than ask "what would you like to clarify?"
- **Status updates with tables** — "where we are at" summaries with structured tables for scannability
- **Honest scope conversations** — when proposing work, give effort estimates + risk of underestimation + multiple paths to choose from
- **Audit-found mistakes called out explicitly** — when I make an error and user catches it, write the mistake into memory + the white paper so future-me doesn't repeat

---

## Reusable templates

Copy-paste-and-fill templates for the most common artifact shapes.

### Template 1: StubXxx fail-loud impl (for Flavor B interfaces)

```csharp
using Microsoft.Extensions.Logging;

namespace {PackageNamespace};

/// <summary>
/// Fail-loud default {InterfaceName} registered by {PackageName}'s
/// AddPolarXxx extension so that DI resolution succeeds even before the real
/// implementation lands under TASK-V20-{NNN}.
/// </summary>
/// <remarks>
/// Every method throws <see cref="NotSupportedException"/> with a message naming
/// the called method, the deferred TASK-V20-{NNN}, and how the host can supply
/// their own implementation. Hosts that need real behavior register their own
/// {InterfaceName} BEFORE calling AddPolarXxx — the registration uses
/// <c>TryAddScoped</c> so the host's wiring wins.
/// </remarks>
internal sealed class Stub{InterfaceName} : {InterfaceName}
{
    private const string TaskReference = "TASK-V20-{NNN}";

    private readonly ILogger<Stub{InterfaceName}> _logger;

    public Stub{InterfaceName}(ILogger<Stub{InterfaceName}> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task<{ReturnType}> {MethodName}({Parameters})
    {
        _logger.LogError(
            "Stub{InterfaceName}.{Method} called but real wiring deferred to {TaskRef} — throwing NotSupportedException.",
            nameof({MethodName}), TaskReference);
        throw new NotSupportedException(
            $"{PackageName}'s default {InterfaceName} is a fail-loud stub; the real implementation is deferred to {TaskReference}. " +
            $"Supply your own {InterfaceName} via services.AddScoped<{InterfaceName}, YourImpl>() BEFORE calling AddPolarXxx, " +
            $"OR wait for the production implementation. Called method: {nameof({MethodName})}.");
    }
}
```

DI registration in the package's `AddPolarXxx` extension:

```csharp
services.TryAddScoped<{InterfaceName}, Stub{InterfaceName}>();
```

Regression test (every Flavor B interface needs this — mirrors the tests in commit `5354e89`):

```csharp
[Fact]
public void AddPolarXxx_registers_a_default_{InterfaceName}_so_DI_resolution_succeeds()
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddPolarXxx(...);
    using var sp = services.BuildServiceProvider();
    var resolved = sp.GetRequiredService<{InterfaceName}>();
    Assert.IsType<Stub{InterfaceName}>(resolved);
}

[Fact]
public async Task Stub_{MethodName}_throws_NotSupportedException_with_TaskRef_pointer()
{
    var stub = new Stub{InterfaceName}(NullLogger<Stub{InterfaceName}>.Instance);
    var ex = await Assert.ThrowsAsync<NotSupportedException>(() => stub.{MethodName}(...));
    Assert.Contains("TASK-V20-{NNN}", ex.Message);
    Assert.Contains("{MethodName}", ex.Message);
}
```

### Template 2: ITenantOwned entity record (immutable; pure data)

```csharp
namespace {PackageNamespace};

/// <summary>
/// {Entity purpose — 1-2 sentence summary}.
/// </summary>
/// <remarks>
/// {Why this entity exists; what feature it supports; cross-references to PLAN.md
/// section and tracking task if applicable.}
/// Standard 5-layer tenant isolation: ITenantOwned + RLS migration on
/// SqlServer/Postgres + per-tenant `.db` (SQLite) + /tenantId partition (Cosmos)
/// + app-layer filter (MariaDB) + single-tenant mode (filter no-op).
/// </remarks>
public sealed record {EntityName} : ITenantOwned
{
    /// <summary>Surrogate primary key.</summary>
    public required Guid Id { get; init; }

    /// <inheritdoc/>
    public required string TenantId { get; init; }

    /// <summary>{Field purpose}.</summary>
    public required {Type} {FieldName} { get; init; }

    // ... additional properties

    /// <summary>UTC when the entity was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
```

Plus: EF Core entity configuration in `Configurations/{EntityName}Configuration.cs` (fluent; no data annotations) + EF migration on each provider + cross-tenant isolation regression test per `CrossTenantIsolationTests` template.

### Template 3: AskUserQuestion option pattern

```json
{
  "label": "Option A — concrete + decisive (Recommended)",
  "description": "What this option means in concrete terms. Why I lean this way: [reasoning]. Tradeoff: [what's gained vs what's lost]. Best when: [context where this is the right call]."
}
```

Patterns:

- Always include `(Recommended)` on the option I'd pick if I were deciding alone
- Always describe tradeoffs in the description, not just features
- "Per-tenant configurable" is often a valid option for the army-of-marketplaces model — surface it where applicable
- Keep options to 2-4 (AskUserQuestion limit)
- When options are similar but differ in scope (e.g. "v1.4.0 with X" vs "v1.4.0 without X"), make the scope difference explicit in label + description

### Template 4: Memory note frontmatter

```markdown
---
name: {Short title; descriptive}
description: {One-line summary used to decide relevance in future conversations}
type: {user | feedback | project | reference}
---

{Memory content. For feedback/project types, structure as:}

**Why:** {The reason the user gave — often a past incident or strong preference}

**How to apply:** {When/where this guidance kicks in}

{Optional: examples, edge cases, exceptions}
```

Type guidance:
- `user` — facts about who the user is, their role, expertise, preferences
- `feedback` — corrections / approved approaches the user has given; behavioral rules
- `project` — facts about the current work, who is doing what, why, by when
- `reference` — pointers to external systems

### Template 5: Subagent dispatch brief

```
{One-paragraph framing of the task.} Repo root: /Users/mollsandhersh/Repos/Polar.sh_Nuget.
Working tree should be {clean/dirty} before you start; you'll leave it {clean/dirty}.

## Why this matters

{1-2 paragraphs of strategic context}

## Read these first (in order)

1. **{File or path}** — {what to look for}
2. **Memory note** {path} — {what context it provides}
3. **{Existing pattern to mirror}** — {pointer to canonical example file}

## Deliverables

### 1. {Concrete deliverable name}

Location: {exact file path}

Shape:
{Code template or specific structure}

Key requirements: {list}

### 2. {Next deliverable}
...

### N. Tests

Add to {test project path}. Specific tests required:
- {Test 1}
- {Test 2}

## Constraints + standards

- AOT-compatible
- CS1591 doc comments
- {Lift-safe / other constraints}
- {Tenant-isolation requirements}

## Verification before reporting

```bash
dotnet build --configuration Release  # 0 warnings 0 errors
dotnet test --configuration Release --no-build --filter "Category!=Integration"  # full unit suite
{specific test command for new work}
```

## Report back in <500 words

- Files modified / created (with line counts)
- Build status
- Test count + pass status
- Judgment calls + gaps
- Working tree state ({commit / leave dirty})
- Surprises during the work
```

### Template 6: Audit subagent brief

See "Audit-before-building pattern" section above for the full template structure. Key fields: read scope (entire src/ tree), search patterns (NotImplementedException, TODO/FIXME, stub/placeholder, suspicious empty bodies, skipped tests, interfaces-without-impls, etc.), classification (A/B/C with definitions), severity ranking of Category C, scope honesty (what was NOT audited).

---

## Reproducibility checklist for new sessions

### Cold-start checklist (read in this order before starting any non-trivial work)

1. **`/Users/mollsandhersh/CLAUDE.md`** — home-level standing requirements (agentic-master policy, RAG policy; applies to all Claude projects)
2. **`/Users/mollsandhersh/Repos/Polar.sh_Nuget/CLAUDE.md`** — this project's standing requirements (build commands, doc standards, framing rules)
3. **`/Users/mollsandhersh/Repos/Polar.sh_Nuget/LARGE-PROJECT-BEST-PRACTICES.md`** — this document (methodology)
4. **`/Users/mollsandhersh/Repos/Polar.sh_Nuget/PLAN.md`** — current technical plan (project-rooted as of 2026-05-19)
5. **`/Users/mollsandhersh/Repos/Polar.sh_Nuget/TASKS.md`** — current tasks (project-rooted as of 2026-05-19)
6. **`/Users/mollsandhersh/Repos/Polar.sh_Nuget/PROGRESS.md`** — completed work log (project-rooted as of 2026-05-19)
7. **`/Users/mollsandhersh/Repos/Polar.sh_Nuget/DECISIONS.md`** — locked architectural decisions (project-rooted as of 2026-05-19)
8. **`/Users/mollsandhersh/AGENTS.md`** — universal cross-project workflow rules (home-rooted; applies across all Claude projects)
9. **`/Users/mollsandhersh/ZoranHorvat.md`** — .NET/C#/NuGet coding standards (home-rooted; applies across all .NET projects)
10. **Case Studies** (`Case Studies/01-*.md` through `05-*.md`) — five canonical architectural patterns referenced throughout the codebase
11. **`.claude/projects/{project-hash}/memory/MEMORY.md`** — memory index (lists all memory notes)
12. **Session-specific memory notes** — read the ones whose descriptions match your current task

### Per-session decision protocol

Before making a non-trivial decision:

- Have you consulted memory notes for any prior decisions on this topic?
- Is there a Case Study that applies?
- Does PLAN.md already capture a decision here?
- If the decision affects new entities: does it satisfy the 5-layer tenant-isolation acceptance criterion?
- If the decision affects DI registration: does it create the audit-found trap pattern (DI-injected interface without default)?
- If the decision claims a framework capability: have you verified via WebSearch?
- If user pushes back on a recommendation: have you considered whether they're right BEFORE digging in?

### Per-implementation protocol

Before writing implementation code:

- Is this in the "safe to stub" or "must implement" category? (See "Stubbing safely" above.)
- Does the new entity satisfy 5-layer tenant isolation? Did you write the cross-tenant regression test?
- Did you use the `StubXxx` canonical pattern for any DI-injected interface that's NOT yet fully implemented?
- Did you add XML doc comments for every public type + member (CS1591 build error)?
- Did you verify AOT compatibility?
- Did you run the full test suite?

### Per-design-session protocol

For substantial design sessions like the v1.4.0 WC catalog work:

- Use AskUserQuestion batches of 1-4 questions with explicit tradeoffs + "Recommended" markers.
- When user rejects with "wants to clarify", ask "what specifically?" with concrete possibilities.
- Capture decisions in the right persistence layer as you go.
- For long walkthroughs, track progress with task list + give the user periodic "where we are at" summaries on request.
- After design is locked, stub the safe-to-stub artifacts (entities + interfaces + registries) while context is fresh.

---

## Agent-friendly framing notes (this document is intentionally machine-parseable)

This document follows several conventions to be useful to both humans and agents:

- **Tables for structured information** — easy to grep, easy to parse.
- **Section headers with clear purposes** — agents can locate the section they need by header.
- **Cross-references to specific artifacts** — `task #66`, `commit 5354e89`, `memory note feedback_tenant_isolation_every_new_entity.md`. Agents can fetch + verify.
- **Reasoning explained alongside every decision** — agents can decide if the reasoning still applies in their context.
- **"Lesson" callouts after each mistake** — explicit takeaway that doesn't require deriving the lesson from the story.
- **Reproducibility checklists** — agents can mechanically follow them.

The white paper is intentionally NOT a tutorial. It assumes the reader has technical chops + can follow links. Its job is to PREVENT REPEATING MISTAKES, not to teach from scratch.

---

## Document maintenance

This document should be UPDATED (not replaced) when:

- A new mistake is made + corrected → add a case study to "Mistakes made + corrections"
- A new architectural pattern emerges → add to "Core methodology" or create a new top-level section
- A standing requirement changes → update the relevant section
- The cold-start checklist artifacts change → update the checklist

Do NOT update when:

- A specific implementation lands → that goes in PROGRESS.md
- A specific task starts → that goes in the task list
- A specific architectural decision is made for a specific feature → that goes in PLAN.md / DECISIONS.md

---

**Last updated:** 2026-05-19 (initial version, captures methodology developed during the v1.4.0 EcommerceStorefronts WebComponents design session).
