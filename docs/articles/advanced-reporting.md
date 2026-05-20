# Advanced reporting (12 reports)

`IAdvancedReportingClient` complements the v1.2.0 `IPolarReportingClient` with twelve additional reports — eight tenant-scoped (for merchant dashboards) and four cross-tenant (for SaaS-operator / AppMasterAdmin oversight). All twelve return `Result<TReport, PolarError>` and read from the snapshot tables populated by `IReportSnapshotService`.

## Tenant reports (8)

Scoped to a single tenant via the global EF query filter. Permission gate: `[RequirePolarPermission(ViewReports)]`.

| Method | Returns | Bucket support |
|---|---|---|
| `GetRevenueOverTimeAsync` | gross / refunded / net revenue + order count per bucket | Daily / Weekly / Monthly |
| `GetTopProductsAsync` | top-N by revenue **and** by units sold | n/a |
| `GetTopCustomersAsync` | top-N by lifetime value with first/last order timestamps | n/a |
| `GetSubscriptionChurnCohortAsync` | per-cohort retention curve over 0–36 months | Monthly cohort |
| `GetRefundRateAsync` | gross / refunded / refund-count per bucket + overall % | Daily / Weekly / Monthly |
| `GetAverageOrderValueAsync` | per-bucket AOV + overall AOV | Daily / Weekly / Monthly |
| `GetCustomerLifetimeValueDistributionAsync` | histogram with caller-defined upper bounds | n/a |
| `GetCurrencyMixAsync` | per-currency gross / net / share % | n/a |

## Operator reports (4)

Cross-tenant. Use `IgnoreQueryFilters()` to escape the tenant scope. Permission gate: `[RequireAppMasterAdmin]` PLUS `[AllowCrossTenant]` — both required, both checked at the endpoint layer.

| Method | Returns |
|---|---|
| `GetCrossTenantRevenueAsync` | platform-wide revenue per tenant + `(other)` rollup beyond top-N |
| `GetCrossTenantOrderVolumeAsync` | order count, distinct customers, last-order timestamp per tenant |
| `GetWebhookDeliveryHealthAsync` | event counts by tenant + type prefix (`order.*`, `subscription.*`, `refund.*`) |
| `GetTenantHealthAsync` | composite grade (`Healthy` \| `Dormant` \| `Empty`) per tenant from customer + order + subscription signals |

## Request shape

```csharp
var report = await client.GetRevenueOverTimeAsync(new RevenueOverTimeRequest
{
    From = DateTimeOffset.UtcNow.AddMonths(-3),
    To = DateTimeOffset.UtcNow,
    Granularity = ReportBucketGranularity.Monthly,
});
```

`ReportBucketGranularity` is one of `Daily` / `Weekly` / `Monthly`. Weekly uses ISO-8601 (Monday at 00:00 UTC) as the bucket start.

## Tenant health grading

| Grade | Criteria |
|---|---|
| `Healthy` | customers > 0 AND recent orders > 0 (within `RecentActivityCutoff`) |
| `Dormant` | customers > 0 AND recent orders = 0 |
| `Empty` | customers = 0 AND recent orders = 0 |

The cutoff is caller-supplied. Typical: 30 days for "active in the last month".

## SQLite limitations

The reference EF impl uses a **materialise-then-filter** pattern for `DateTimeOffset` range queries: SQLite's EF Core provider cannot translate `o.CreatedAt >= request.From` when combined with the global tenant filter's parameterised expression. The fix materialises post-tenant-filter and narrows in memory. SQL Server / PostgreSQL providers translate the same query server-side natively.

The default `ListCustomersAsync` sort is `LastOrderAt` (DateTimeOffset?) which SQLite cannot translate. Pass `SortBy = "Email"` / `"OrderCount"` / `"LifetimeValue"` / `"CreatedAt"` for SQLite. Production providers translate the default sort fine.

## Permission semantics

```csharp
app.MapGet("/admin/reports/revenue-over-time", ...)
    .RequirePolarPermission(PolarPermission.ViewReports);              // tenant-scoped

app.MapGet("/platform/reports/cross-tenant-revenue", ...)
    .RequireAppMasterAdmin()                                            // site-level gate
    .AllowCrossTenant();                                                // EF + RLS bypass opt-in
```

Without `[AllowCrossTenant]`, even an AppMasterAdmin sees only their current-tenant scope. Routes that intentionally cross tenant boundaries declare it visibly.

## What the operator reports do NOT include

- They do not show **individual customer records** across tenants — that's against the SaaS-tenant trust model. Customer-level data stays inside the tenant's own dashboard.
- They aggregate at the **tenant** level: counts, sums, grades, last-event timestamps. The platform operator sees "Tenant A has 142 active subscribers and last received a webhook 3 minutes ago", not "User alice@x.com purchased Product Y for $30".

## Performance posture

Snapshot tables are indexed for these queries: `(tenant_id, created_at)` on orders, `(tenant_id, last_order_at)` on customers, `(tenant_id, occurred_at)` on events. On SQL Server / PostgreSQL the full 12 reports complete server-side; SQLite uses the in-memory narrowing pattern. Worth noting: the in-memory materialisation cost on production providers is a v2.0 polish item (the SQLite-compat pattern was applied uniformly during v1.3.H; SQL Server / PostgreSQL hosts should rewrite the same queries to keep the filter server-side). Tracked in [PRODUCTION-READINESS-ANALYSIS.md](https://github.com/MollsAndHersh/Polar.sh_Nuget/blob/main/PRODUCTION-READINESS-ANALYSIS.md).
