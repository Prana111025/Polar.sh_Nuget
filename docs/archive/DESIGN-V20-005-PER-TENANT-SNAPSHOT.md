# DESIGN-V20-005 — Per-tenant snapshot trigger + 13-resource scope

**Status:** Proposal awaiting project-owner review.
**Author:** Claude (Opus 4.7).
**Last updated:** 2026-05-13.
**Replaces:** the original V20-005 plan in `~/TASKS.md` (time-based BackgroundService sweeping all tenants every 15 min).

---

## Why this design changes

The original V20-005 plan called for a `PolarReportingHostedService` that polls every tenant in `IMultiTenantStore` every 15 minutes. That gives consistent freshness but scales poorly: 100 tenants × 13 resources × ~10 pages/page-size avg = ~13k Polar API calls per tick, ~52k per hour. Beyond unknown rate-limit risk, ~80% of that work is wasted — most tenants aren't logged in and nobody's looking at their dashboard.

The redesign: **snapshot work happens for the tenant that is actively using the app, on demand and on a short cron tied to their session.** API call volume scales with concurrent active users, not customer count. A tenant with no active session contributes zero API calls.

This is a pattern many SaaS dashboards already use (Stripe, Linear, GitHub all show "data refreshed N seconds ago / refreshing in M seconds / [Refresh now]"). It also matches the user attention model — fresh data only matters when someone's looking at it.

## Snapshot scope — 13 resources

Per the project owner's "more data = more reporting" decision (2026-05-13), the snapshot ingests every Polar resource that has a paginated GET list endpoint with non-trivial analytical value. Files excluded (media assets, no analytical use).

| # | Polar resource | Endpoint | EF entity | Reports it powers |
|---|---|---|---|---|
| 1 | Events | `GET /v1/events/` | `ReportEventEntity` (exists) | WebhookDeliveryHealth (operator), error-audit, raw event log |
| 2 | Orders | `GET /v1/orders/` | `ReportOrderEntity` (exists) | RevenueOverTime, AOV, RefundRate, CurrencyMix, TopProducts, drilldown |
| 3 | Order line items | (nested in orders) | `ReportOrderLineItemEntity` (exists) | TopProducts (units sold), drilldown line-item view |
| 4 | Order refunds | (nested in orders) | `ReportOrderRefundEntity` (exists) | RefundRate, drilldown refund view |
| 5 | Subscriptions | `GET /v1/subscriptions/` | `ReportSubscriptionEntity` (exists) | SubscriptionChurnCohort, MRR/ARR (future) |
| 6 | Customers | `GET /v1/customers/` | `ReportCustomerEntity` (exists) | TopCustomers, LTV Distribution, drilldown customer list |
| 7 | Refunds (top-level) | `GET /v1/refunds/` | (uses `ReportOrderRefundEntity`) | de-duplicated against the nested-in-orders ingestion (same Polar id = same row) |
| 8 | **Benefits** | `GET /v1/benefits/` | **NEW** `ReportBenefitEntity` | Active benefit catalog, benefit-by-type rollup |
| 9 | **Benefit grants** | `GET /v1/benefit-grants/` | **NEW** `ReportBenefitGrantSnapshotEntity` (the existing `ReportBenefitGrantEntity` is order-scoped; this one is the standalone Polar resource) | Entitlement audit, retention-by-benefit |
| 10 | **Discounts** | `GET /v1/discounts/` | **NEW** `ReportDiscountEntity` | Discount usage, redemption rate, expiry pipeline |
| 11 | **Checkout links** | `GET /v1/checkout-links/` | **NEW** `ReportCheckoutLinkEntity` | Conversion funnel ("which links convert best") |
| 12 | **Products** | `GET /v1/products/` | **NEW** `ReportProductEntity` | Polar-side catalog drift detection (vs the host's local catalog maintained by `PolarCatalogPublisher`) |
| 13 | **License keys** | `GET /v1/license-keys/` | **NEW** `ReportLicenseKeyEntity` | License utilization, expiry pipeline, activation count rollup |
| 14 | **Meters** | `GET /v1/meters/` | **NEW** `ReportMeterEntity` | Usage-based billing meter definitions catalog |
| 15 | **Customer meters** | `GET /v1/customer-meters/` | **NEW** `ReportCustomerMeterEntity` | Per-customer usage tallies, billing-period aggregates |

**Why 15 entities, not 13:** orders carry nested line-items + refunds, so the "13 paginated GETs" expand to 15 EF entities. Implementation-wise the line-items + refunds are upserted as part of the order pass (one HTTP call, multiple entity writes), so the per-tick API cost stays at 13.

**Skipped:** Files (media-asset catalog, low analytical value), Organizations (single-org-per-token; covered by V20-004's per-call GET).

## IReportSnapshotTrigger — public surface

New public interface in `PolarSharp.Reporting`:

```csharp
namespace PolarSharp.Reporting.Snapshot;

/// <summary>
/// Per-tenant snapshot driver. Hosts call this to (a) fire a one-shot snapshot when a tenant
/// becomes active (login, page-mount, etc.), (b) start/stop a per-tenant periodic snapshot
/// while the tenant is actively using the app, (c) query "when was data last refreshed?"
/// for the UI freshness indicator, and (d) honor a manual "Refresh now" button.
/// </summary>
public interface IReportSnapshotTrigger
{
    /// <summary>
    /// Fire an immediate snapshot for the tenant. Idempotent — concurrent calls for the same
    /// tenant deduplicate against the in-flight snapshot. Returns when the snapshot completes
    /// (or fails). For fire-and-forget UX, the caller can ignore the returned Task.
    /// </summary>
    /// <param name="tenantId">Tenant whose data to snapshot.</param>
    /// <param name="reason">"Login" / "PageMount" / "ManualRefresh" / "PostMutation" — captured in the
    /// resulting <see cref="SnapshotCompletedEvent"/> for diagnostics. Free-form short string.</param>
    Task<SnapshotCompletedEvent> TriggerImmediateAsync(TenantId tenantId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Begin periodic per-tenant snapshotting on the supplied interval. Repeats until
    /// <see cref="StopPeriodicAsync"/> is called or the idle-timeout elapses since the last
    /// activity heartbeat. Calling on an already-active tenant resets the idle clock and
    /// updates the interval if different. The first tick fires immediately (so callers don't
    /// need to also call <see cref="TriggerImmediateAsync"/>).
    /// </summary>
    Task StartPeriodicAsync(TenantId tenantId, TimeSpan interval);

    /// <summary>
    /// Stop the periodic poll for the tenant. Idempotent — calling for an unknown tenant is a
    /// no-op. Any in-flight snapshot finishes naturally; no future ticks are scheduled.
    /// </summary>
    Task StopPeriodicAsync(TenantId tenantId);

    /// <summary>
    /// Heartbeat — the host's UI middleware/SignalR hub calls this on every authenticated
    /// request (or every N seconds while a reporting page is open) to signal "this tenant is
    /// still actively using the app." Resets the idle-timeout clock; without it, periodic
    /// polling auto-stops after <see cref="SnapshotTriggerOptions.IdleTimeout"/>.
    /// </summary>
    void Heartbeat(TenantId tenantId);

    /// <summary>UTC timestamp of the most recent successful snapshot for the tenant, or null if none yet.</summary>
    DateTimeOffset? GetLastSnapshotAt(TenantId tenantId);

    /// <summary>Time until the next scheduled tick for the tenant, or null if not currently polled.</summary>
    TimeSpan? GetTimeUntilNextSnapshot(TenantId tenantId);

    /// <summary>
    /// Stream of completion events the host's SignalR/IPolarToastChannel can subscribe to.
    /// One event per completed snapshot tick (success or failure) so the UI can update the
    /// "last refreshed at … / next in …" header in real time.
    /// </summary>
    IAsyncEnumerable<SnapshotCompletedEvent> CompletedEvents(CancellationToken ct);
}

public sealed record SnapshotCompletedEvent(
    TenantId TenantId,
    DateTimeOffset CompletedAt,
    string Reason,
    bool Success,
    int ResourcesIngested,        // count of resources that had non-zero deltas
    int RowsIngested,             // total rows upserted across all resources
    TimeSpan Duration,
    string? ErrorMessage = null);

public sealed class SnapshotTriggerOptions
{
    /// <summary>Default per-tenant polling cadence when StartPeriodicAsync is called without an explicit interval.</summary>
    public TimeSpan DefaultInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Stop periodic polling for a tenant if no Heartbeat() has been received in this window.
    /// Default 30 min — covers a closed-laptop / lost-session scenario without churning API
    /// quota for an absent user.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Minimum interval between manual TriggerImmediateAsync(reason: "ManualRefresh") calls
    /// per tenant. Excess calls during the cooldown return the most-recent completion event
    /// without firing a new snapshot. Prevents button-mashing from burning API quota.
    /// </summary>
    public TimeSpan ManualRefreshDebounce { get; set; } = TimeSpan.FromSeconds(30);
}
```

**Internal impl shape:** `PerTenantSnapshotOrchestrator` holds a `ConcurrentDictionary<TenantId, TenantSnapshotState>` where `TenantSnapshotState` carries the periodic-tick `Timer`, an in-flight semaphore (1-permit, for dedup), the last-snapshot-at timestamp, and the last-heartbeat timestamp. Each tick: check heartbeat freshness → bail if idle-timeout elapsed → take semaphore → run `RunSnapshotForTenantAsync(tenantId, reason)` → release. Completion event published to a `Channel<SnapshotCompletedEvent>` whose reader is the source for `CompletedEvents()`.

## The 5 corners — proposed defaults

| # | Corner | Proposed default | Why |
|---|---|---|---|
| 1 | **Login event source** | Explicit `OnLogin` / `OnLogout` callbacks the host's auth handler invokes — `IReportSnapshotTrigger.StartPeriodicAsync(tenantId, default)` on login, `StopPeriodicAsync(tenantId)` on logout | Most explicit; matches how Identity-package hooks already signal lifecycle events. The MOR-aware reporting page can ALSO call `Heartbeat(tenantId)` on every request via a middleware so the polling stays alive while the tenant is browsing reports specifically. The `OnLogin` hook is the trigger; the heartbeat is the keepalive |
| 2 | **Idle timeout** | 30 min (configurable via `SnapshotTriggerOptions.IdleTimeout`) | Long enough that a coffee break doesn't kill polling; short enough that a closed laptop doesn't burn quota for hours. Standard SaaS-session timeout window |
| 3 | **Cross-tenant operator reports** | Trigger on-demand when AppMasterAdmin opens an operator report. `TriggerImmediateAsync(tenantId, "OperatorReport")` fired per-tenant via `Parallel.ForEachAsync(MaxDegreeOfParallelism = 4)` over `IMultiTenantStore.GetAllAsync()`. Total cost = N × 13 paginated GETs once per page-load, debounced for 5 min. PLUS a "data-freshness per tenant" column in operator reports so operators can see which rows are stale | Avoids the time-based platform sweep entirely. Operator reports are a low-frequency activity (a few page-loads per day, not hundreds), so on-demand stays bounded |
| 4 | **Multi-instance dedup** | Per-tenant 1-permit `SemaphoreSlim` inside `TenantSnapshotState`. Two browser tabs or two app pods triggering the same tenant → only one snapshot in flight; the other waits and returns the same `SnapshotCompletedEvent` when it finishes | Simple, in-process. For multi-pod hosts (web farm), this dedups WITHIN one pod but two pods can still each start a snapshot. v2.0 design caveat: cross-pod dedup needs distributed lock (Redis SETNX or similar) — flagged for v2.x as `TASK-V20-018`. For v1 this is acceptable because (a) most hosts are single-pod, (b) Polar idempotency on GETs means double-calls are wasteful but not destructive, and (c) the snapshot tick is short enough that overlap windows are tiny |
| 5 | **Refresh-now debounce** | 30 sec per tenant (configurable via `SnapshotTriggerOptions.ManualRefreshDebounce`). Excess clicks during cooldown return the most-recent `SnapshotCompletedEvent` without firing a new tick. The UI button can show a countdown ("Available in 23 sec") to set expectations | Prevents button-mashing without making the button feel sluggish. 30 sec is short enough that a deliberate retry isn't blocked, long enough that accidental double-clicks don't double-call |

## SignalR / UI affordances (v1.4 RCL — design only here, no impl)

These are the components the v1.4 `PolarSharp.UI.Components.Web` package will ship. Documented here so the trigger interface is shaped to support them; the components themselves land in v1.4.

**`PolarSnapshotFreshnessHeader` Razor component.** Drops onto any reporting page. Renders:

```
Last refreshed: 2:34:18 PM (3 min ago)    Next refresh in: 12 min    [↻ Refresh now]
```

Subscribes to `IReportSnapshotTrigger.CompletedEvents()` via SignalR (the existing `IPolarToastChannel` pattern) and re-renders the freshness line every time a tick completes for the current tenant. The "[↻ Refresh now]" button calls `TriggerImmediateAsync(currentTenantId, "ManualRefresh")` and shows a brief spinner state while the snapshot runs.

**`PolarSnapshotPageMountTrigger` Razor component.** Invisible component placed inside reporting page layouts. On mount: `StartPeriodicAsync(currentTenantId, TimeSpan.FromMinutes(15))`. On unmount: `StopPeriodicAsync(currentTenantId)`. Optional override for shorter intervals on critical pages.

**Heartbeat middleware.** ASP.NET Core middleware that calls `IReportSnapshotTrigger.Heartbeat(currentTenantId)` on every authenticated request when a tenant context is in scope. Costs ~100 ns per request; keeps the idle clock fresh while the user is using the app at all (not just reporting pages).

**Operator report freshness indicator.** When AppMasterAdmin opens an operator report, the page calls `TriggerImmediateAsync` per-tenant (debounced) and shows per-row "fresh as of …" timestamps so they can see which tenants have stale data.

## Implementation phases (post-spec-approval)

1. **Phase 1 — Snapshot ingestion for the 13 resources** (~3 hours):
   - 8 new EF entities + 8 new EF configurations + migrations across 3 SQL providers (24 migrations total)
   - 8 new `IPolarReportingApi.Fetch*SinceAsync` methods + paginated impls in `PolarClientReportingApi`
   - Live integration tests gated on `POLAR_SANDBOX_TOKEN` per resource

2. **Phase 2 — Per-tenant trigger orchestrator** (~1.5 hours):
   - `IReportSnapshotTrigger` interface + `PerTenantSnapshotOrchestrator` impl
   - `SnapshotTriggerOptions` config binding
   - Replace existing `PolarReportingHostedService` time-sweep with on-demand trigger semantics; old hosted service retained as opt-in "platform sweep" mode for hosts that want time-based behavior
   - Unit tests for: dedup semaphore, idle-timeout, manual-refresh debounce, completion event publishing

3. **Phase 3 — Identity hook + test app integration** (~30 min):
   - `OnLogin` / `OnLogout` callbacks in `PolarSharp.MultiTenant.Identity` that call the trigger
   - `Heartbeat` middleware in `PolarSharp.MultiTenant.EntityFrameworkCore`
   - PolarTestApp wires the lifecycle so the trigger is exercised end-to-end against the live sandbox

4. **Phase 4 — Tag `v2.0.0-preview-2`** marking the snapshot redesign as the second milestone of the v2.0 cycle.

5. **Phase 5 (deferred to v1.4)** — RCL components: `PolarSnapshotFreshnessHeader`, `PolarSnapshotPageMountTrigger`. Lands alongside the `PolarSharp.UI.Components.Web` package per the existing v1.4 plan.

## What's intentionally out of scope

- **Cross-pod distributed dedup** — flagged as `TASK-V20-018` for v2.x. Not blocking for single-pod or low-instance-count hosts
- **WebSocket/SSE snapshot streaming directly to the browser** — current design uses the existing `IPolarToastChannel` SignalR bridge. A direct browser-to-snapshot WebSocket would skip the SignalR hub, but that's a v1.4 RCL design call, not a v2.0 reporting concern
- **Tenant-driven custom polling intervals** — for v2.0 the host can call `StartPeriodicAsync` with any interval, but there's no per-tenant "preferred interval" stored in `TenantBusinessProfile`. v2.x could add this as a tenant setting
- **Snapshot priorities / SLA tiers** — for hosts with paid-vs-free tenant tiers wanting different polling cadences. v2.x consideration

## Open questions for project owner

1. **Login hook location** — should it live in `PolarSharp.MultiTenant.Identity` (cleanest, but creates a soft dep from Reporting to Identity) or in a new `PolarSharp.Reporting.Identity` adapter package (more decoupled but more package surface)? My lean: small adapter package, optional install, keeps Reporting independent of Identity for hosts that use a different auth stack
2. **Should the `Heartbeat` middleware ship in `PolarSharp.MultiTenant.EntityFrameworkCore` or a new tiny adapter?** Same trade-off as #1
3. **Operator-report on-demand trigger** — should the debounce window for operator-report-driven snapshots be per-AppMasterAdmin-user or platform-wide? My lean: platform-wide (simpler, and operator reports are inherently low-frequency)
4. **Is the per-tenant `OrganizationsApi.GetAsync` from V20-004 considered part of this snapshot scope, or stays out-of-band as the payout-status poller already designs?** My lean: stays out-of-band. The payout-status poll runs at its own cadence (5 min when InProgress, never when Ready); folding it into the snapshot would over-poll Ready tenants

---

## Approval prompt

If you approve this spec as written, I'll proceed with Phase 1 implementation immediately. If you want changes — different defaults on any of the 5 corners, different scope on the 13 resources, different staging across the 5 phases — flag them and I'll revise the spec before any code lands.
