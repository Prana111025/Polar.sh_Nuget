# DESIGN-V20-020 — Memory-leak audit plan

**Status:** Proposal awaiting project-owner review.
**Author:** Claude (Opus 4.7).
**Last updated:** 2026-05-14.
**Scheduled:** v2.0 Pillar 2 (production hardening), early — runs once as a sweep, then becomes a standing CI gate.

---

## Why this audit exists

PolarSharp ships several long-lived components that hold unmanaged or scarce resources: timers, semaphores, channels, cancellation token sources, HTTP connection pools, EF Core connection pools, distributed-cache clients. A host application that runs PolarSharp for weeks at a time MUST be confident that:

1. **Steady-state memory doesn't grow unboundedly** — no per-tenant or per-request state accumulates without an eviction path
2. **Graceful shutdown cleans up disposable resources** — when the host calls `IHost.StopAsync()`, all `IAsyncDisposable` / `IDisposable` services release their handles within seconds
3. **Crash / SIGKILL doesn't matter** — the OS reclaims process memory; this audit doesn't try to fix that case (impossible by definition)
4. **Hot-reload in development doesn't double-allocate** — `dotnet watch` rebuilds the DI container; old singletons must Dispose, new ones must initialize cleanly

This audit was prompted 2026-05-14 by the project owner asking: *"I would like to have any of my concerns that the triggering mechanisms aren't going to create any memory leaks if the host apps suddenly quits."* — specifically about V20-005 Phase 2's `PerTenantSnapshotOrchestrator`, but the concern generalizes.

## The 8 leak patterns we audit for

| # | Pattern | Detection | Risk |
|---|---|---|---|
| 1 | **Undisposed `IDisposable` field / property** stored on a long-lived class — `Timer`, `SemaphoreSlim`, `CancellationTokenSource`, `HttpClient`, etc. | Grep for `new Timer\|new SemaphoreSlim\|new CancellationTokenSource` and verify each has a paired Dispose path | Per-instance ~hundreds of bytes; per-tenant unbounded |
| 2 | **Unbounded `ConcurrentDictionary` / `Dictionary` / `List` growth** — caller adds entries; nothing removes them | Grep for `.TryAdd\|.Add(` on singleton-held collections; check for paired `.TryRemove\|.Remove` OR an eviction policy (TTL, LRU) | High — accumulates over uptime |
| 3 | **Fire-and-forget `Task.Run` / `_ = SomeAsyncCall()`** that may never complete | Grep for `_ = Task\.\|_ = .*Async\b`; verify each has a cancellation token tied to the host's stopping token | Medium — tasks pin GC roots until completion |
| 4 | **Event handler subscription without unsubscription** — `something.Event += handler;` where the publisher outlives the subscriber | Grep for `\+= ` on event/delegate; check for `-= ` in disposal | High — classic event leak; subscriber kept alive by publisher |
| 5 | **Static collections / caches without eviction** — `private static readonly Dictionary<...>` accumulating forever | Grep for `static.*Dictionary\|static.*ConcurrentDictionary\|static.*List\|static.*Cache` and verify each has a cap or TTL | High — outlives the entire DI container |
| 6 | **`HttpClient` created with `new HttpClient()`** instead of `IHttpClientFactory` | Grep for `new HttpClient\(`; verify each is either (a) injected via factory, (b) used in a `using` block, or (c) is an unavoidable test fixture | Medium — socket exhaustion under high traffic |
| 7 | **EF Core `DbContext` retained beyond scope** — captured into a singleton's closure or stashed on a field | Grep for fields of type `DbContext` (or derived) on classes registered as `Singleton` lifetime | High — pins entire connection pool |
| 8 | **`IServiceScope` not disposed** — manually created via `IServiceScopeFactory.CreateScope()` without `using` | Grep for `CreateScope()` and verify each is in a `using` block or assigned to a `using var` | High — scope holds Scoped services + transient roots |

## Audit scope

Run the audit against every class in `src/` matching ANY of these:

- Registered as `ServiceLifetime.Singleton` in any `Add*` extension
- Implements `IHostedService` / `BackgroundService` / `IHostedLifecycleService`
- Stored in a `[ThreadStatic]` or `[AsyncLocal]` field
- Has any `static` field of type `ConcurrentDictionary` / `Dictionary` / `List` / `Cache`

**Out of scope** (these don't leak in practice):
- Scoped services — DI container disposes them at end-of-scope
- Transient services — caller-owned lifetime
- Records, value objects, immutable types — no resource ownership
- Test fixtures — short-lived; not production code

## Initial inventory of audit targets

A first-pass `grep` over `src/` finds these candidates for review:

| Class | Lifetime | Resources held | Priority |
|---|---|---|---|
| `PerTenantSnapshotOrchestrator` (V20-005 Phase 2) | Singleton | Timer + SemaphoreSlim + Channel + CTS + ConcurrentDictionary | **HIGH** — just added; primary motivation for this audit |
| `MemoryPolarTenantCache` (PolarSharp.MultiTenant.EntityFrameworkCore) | Singleton | `IMemoryCache` wrapper | MEDIUM — `IMemoryCache` handles eviction; verify no stale GC roots |
| `MemoryPolarCatalogTranslationCache` (PolarSharp.EcommerceStoreManagement) | Singleton | `IMemoryCache` wrapper | MEDIUM — same shape |
| `IInventoryEventNotifier` impl (PolarSharp.EcommerceStoreManagement.EntityFrameworkCore) | Singleton | `Channel<SkuStockChanged>` writer | MEDIUM — channel bounded by config |
| `PolarWebhookBackgroundService` (PolarSharp.Webhooks) | IHostedService | Background Task + Channel reader | MEDIUM — check graceful shutdown semantics |
| `FakeDataSyncService` (PolarSharp.DataSeeding) | IHostedService | Background Task + Channel reader | MEDIUM — same shape |
| `AppMasterAdminBootstrapper` (PolarSharp.MultiTenant.Identity) | IHostedService | Runs once at startup; should not retain state | LOW |
| `TenantAdminInvariantValidator` (PolarSharp.MultiTenant.Identity) | IHostedService | Runs once at startup | LOW |
| `OnboardingSessionExpirationCleaner` (PolarSharp.Onboarding) | IHostedService | Background Task | MEDIUM |
| `PayoutStatusPollerService` (PolarSharp.EcommerceStoreManagement, if registered) | IHostedService | Timer + per-tenant state | MEDIUM |
| Kiota-generated `HttpClient` factory wiring in `PolarSharp` core | DI | Pooled HttpMessageHandler | MEDIUM — verify `IHttpClientFactory` usage |
| Tests' `ReportingTestContext` / `CatalogTestContext` | Test fixture | SqliteConnection + ServiceProvider | LOW — tests dispose via `IAsyncDisposable` |

## Methodology

### Phase A — manual code review (1 hour per top-priority class)

For each HIGH/MEDIUM priority class:

1. Read every field on the class. For each field, ask:
   - Is the field type `IDisposable` / `IAsyncDisposable`? If yes → is there a Dispose path that releases it?
   - Is the field a collection? If yes → is there an eviction path?
   - Is the field a `Task` / `CancellationTokenSource`? If yes → is it canceled in Dispose?
2. Read every method. For each method, look for:
   - `new SomethingDisposable(...)` without `using` or paired Dispose
   - `CreateScope()` without `using`
   - Event subscriptions (`+=`) without corresponding unsubscription
   - `Task.Run(...)` / `_ = SomeAsync()` without proper cancellation
3. Check the registration extension:
   - Singleton lifetime? Confirm Dispose is wired (DI container calls it)
   - IHostedService? Confirm `StopAsync` releases resources

### Phase B — automated grep sweeps (~30 min)

Run these regex sweeps over the codebase. Each potential leak gets an issue ticket:

```bash
# Pattern 1: undisposed IDisposable creation in non-test code
grep -rn "new \(Timer\|SemaphoreSlim\|CancellationTokenSource\|HttpClient\)\b" src/ --include="*.cs"

# Pattern 3: fire-and-forget patterns
grep -rn "^\s*_ = .*Async\|^\s*Task\.Run\(" src/ --include="*.cs"

# Pattern 4: event subscription
grep -rn "\+= \|EventHandler" src/ --include="*.cs"

# Pattern 5: static state
grep -rn "static \(readonly \)\?Dictionary\|static \(readonly \)\?ConcurrentDictionary\|static \(readonly \)\?List" src/ --include="*.cs"

# Pattern 6: raw HttpClient
grep -rn "new HttpClient(" src/ --include="*.cs"

# Pattern 8: undisposed CreateScope
grep -rn "CreateScope()" src/ --include="*.cs"  # then manually verify each is in `using`
```

### Phase C — runtime profiling (~2 hours, opt-in)

For HIGH priority classes:

1. Build a stress-test harness that hammers the class for 60 seconds simulating realistic load (e.g., for `PerTenantSnapshotOrchestrator`: 1000 random tenants firing TriggerImmediate + StartPeriodic + StopPeriodic in a Poisson distribution)
2. Use `dotnet-counters monitor` to watch:
   - `dotnet.gc.heap.size` — should stabilize, not grow linearly
   - `dotnet.threadpool.thread.count` — should stay bounded
   - `dotnet.runtime.allocations.size` — high allocations OK, retention is the concern
3. Use `dotnet-gcdump collect` mid-stress and post-stress. Compare. Any retained `Timer` / `SemaphoreSlim` / `TenantState` counts that don't match expected concurrent-tenant count = leak
4. Use `dotMemory` for visual heap analysis if available

### Phase D — CI gate (~1 hour to set up)

Add a `.github/workflows/memory-leak-sweep.yml` workflow that:

1. Runs the Phase B greps as a script
2. Fails the build if any new match appears that isn't on an `audit/EXEMPTIONS.md` allowlist
3. Optionally runs the Phase C stress harness on a daily schedule (not per-PR — too expensive); posts the dotnet-counters time-series to a CI artifact for trend review

## Tooling shortlist

| Tool | Use | Cost |
|---|---|---|
| `dotnet-counters` | Live metrics during stress | Free, built into SDK |
| `dotnet-gcdump` | Snapshot heap state | Free, built into SDK |
| `dotMemory` (JetBrains) | Visual heap analysis | Paid; trial available |
| `BenchmarkDotNet` MemoryDiagnoser | Per-operation allocation tracking | Free; already used in some PolarSharp benchmarks |
| `Microsoft.Diagnostics.Runtime` | Programmatic heap inspection | Free, NuGet |

## Acceptance criteria

- [ ] Every class on the HIGH priority list has a written Phase A review (one paragraph per class in `audit/MEMORY-AUDIT-FINDINGS.md`)
- [ ] Every class on the MEDIUM priority list has a written Phase A review (less depth OK)
- [ ] Phase B grep sweeps run; every match is either fixed, allowlisted with a rationale, or ticketed as a follow-up
- [ ] `PerTenantSnapshotOrchestrator` passes Phase C (heap-snapshot stress test) with stable retained TenantState count matching active-tenant count, no orphan Timer/Semaphore retention
- [ ] CI gate (Phase D) running on every PR; existing matches grandfathered via `audit/EXEMPTIONS.md`; new matches block the PR

## Specific fixes already applied in this commit

Per the user's prompt, I caught three immediate leaks in `PerTenantSnapshotOrchestrator` (the V20-005 Phase 2 orchestrator I just wrote):

1. **`StopPeriodicAsync` was disposing the Timer but leaving the `TenantState` entry in the dict** — the `SemaphoreSlim` owned by the state was never disposed, leaking ~500-800 bytes per stop. **Fixed:** `StopPeriodicAsync` now does `_tenants.TryRemove` + `state.Semaphore.Dispose()` to drop the entry entirely.
2. **Idle-timeout calls `StopPeriodicAsync` internally** — inherits the same fix.
3. **`_tenants` dict had no upper bound** — addressed by #1 + #2 (entries get removed on every termination path), but a future v2.x enhancement could add an LRU cap as a safety net.

These fixes ship in this commit; the audit doc captures the methodology for catching others like them.

## Open questions for project owner

1. **CI gate enforcement** — should Phase D fail the build on new matches, or warn and ticket? My lean: warn-and-ticket initially (low friction); upgrade to fail after we've burned down the first wave of false positives in `EXEMPTIONS.md`.
2. **Stress-test cadence** — Phase C against `PerTenantSnapshotOrchestrator` is high-value; should we run it on every PR (~5 min added to CI), nightly only, or weekly? My lean: nightly. Per-PR is overkill for memory tests since they typically catch regressions only after enough load accumulates.
3. **Scope for v2.0** — must-haves before v2.0 ships are Phase A on the HIGH list + Phase B sweep. Phase C + Phase D can defer to v2.x without blocking the v2.0 release.

## Approval prompt

If approved as written, this gets logged into `~/TASKS.md` as TASK-V20-020 with Phases A–D as sub-tasks; the Phase A reviews + Phase B sweep are scheduled before v2.0 tag. Phase C + Phase D can defer.
