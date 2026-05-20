# Catalog publisher

`PolarCatalogPublisher` in `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore` ships the local catalog (products, variants, tiers, benefits, discounts, checkout links) to Polar. Idempotent, dependency-ordered, resumable.

## Two-call shape

```csharp
public interface IPolarCatalogPublisher
{
    Task<Result<PublishPlan, PolarError>> PreviewAsync(TenantId tenantId, CancellationToken ct = default);
    Task<Result<PublishReport, PolarError>> PublishAsync(TenantId tenantId, PublishOptions options, CancellationToken ct = default);
}
```

**Always preview first.** `PreviewAsync` returns `PublishPlan` enumerating every `PlannedAction` (`CreatePolarProduct`, `UpdatePolarProduct`, `ArchivePolarProduct`) with counts (`CreateCount`, `UpdateCount`, `ArchiveCount`). Hosts surface the plan to operators before pressing **Publish**.

## Dependency order

The publisher walks the graph in this order:

1. **Benefits** — POST `/v1/benefits/`. Products that attach benefits need them by Polar id.
2. **Products** (with variant + tier expansion) — POST or PATCH `/v1/products/`.
3. **Discounts** — POST `/v1/discounts/` referencing fresh Polar product ids.
4. **Checkout links** — POST `/v1/checkout-links/` referencing fresh Polar product ids.

Failures mid-step leave preceding successful writes intact; the next `PublishAsync` resumes from `OutOfSync` rows.

## Idempotency

`LocalProduct.PolarProductId` is the durable mapping:
- `null` → POST (create); the returned id is persisted before the next iteration.
- non-null → PATCH (update).
- Removed local variants → `is_archived: true` on the corresponding Polar product. Polar has no DELETE.

`LocalProduct.Status` transitions: `Draft` → `Published` on success; `Failed` on error; `OutOfSync` if the local row changes after publish but before the next sync.

## Variant + tier expansion

- A `LocalProduct` with `HasVariants=true` and N variants → N Polar Products, names templated as `{MasterName} — {axes-joined}` (e.g. "Premium T-Shirt — Red, M"). Each carries `Metadata["polar_sharp_parent_id"]` for round-trip identification.
- A `LocalTierGroup` with N tiers → N Polar Products with cumulative benefit bundles (`Levels[0..n].AddedBenefits.SelectMany(...)` per tier). Each carries `Metadata["polar_sharp_tier_group_id"]` + `"polar_sharp_tier_rank"`.
- Variants marked `IsActive=false` OR `InventoryCount<=0` publish with `is_archived: true` (hidden from Polar's checkout).

## Dry-run

`PublishOptions.DryRun = true` runs the plan computation without HTTP calls. Useful for CI checks and admin-confirm UIs.

## Partial-failure semantics

The publisher writes outcomes per-product as it goes. A network drop at item 7 of 20 leaves items 1–6 marked `Published`, items 7–20 marked `OutOfSync`. Resume on the next `PublishAsync(scope: AllOutOfSync)`. Network conditions on Polar's side are handled by PolarSharp's existing circuit breaker + retry-with-jitter resilience pipeline.

## v2.0 deferral

The concrete `IPolarPublishingApi` impl behind `PolarClientPublishingApi` is a deferred stub (TASK-V20-001) until the Kiota request builders are wired through `PolarClient`. The plan computation, dependency-ordering, variant expansion, and `Metadata` tagging all run today; the actual HTTP calls are no-ops returning `UnexpectedFailure`. Hosts with a custom `IPolarPublishingApi` against the live Polar API get end-to-end publishing today.

## Known limitation — single SaveChanges

`PublishAsync` currently commits all `PolarXxxId` mutations in one `SaveChangesAsync` at the end of the run. A process crash mid-loop loses the durable Polar-id mapping (Polar got the calls; the local DB doesn't know). Tracked for v2.0 as TASK-V20-008 — per-action transaction boundaries with optimistic-concurrency safeguards. Documented in [PRODUCTION-READINESS-ANALYSIS.md](https://github.com/MollsAndHersh/Polar.sh_Nuget/blob/main/PRODUCTION-READINESS-ANALYSIS.md).
