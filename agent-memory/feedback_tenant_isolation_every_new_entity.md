---
name: Every new tenant-scoped entity must honor PolarSharp's 5-layer isolation
description: When proposing new backend features / entities, explicitly call out ITenantOwned + RLS + SQLite + Cosmos + MariaDB + single-tenant treatment
type: feedback
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
When proposing new backend features for PolarSharp (new entities, new tables, new columns, new indexes), do NOT just describe the entity shape. Every new entity must explicitly satisfy PolarSharp's 5-layer tenant-isolation policy:

1. **`ITenantOwned`** interface implementation (carries `TenantId`) so `TenantAwareDbContextBase` auto-applies the global query filter
2. **Database RLS policy** on SqlServer + Postgres provider packages (EF migration ships the policy in the initial migration; defense in depth against raw-SQL / bug bypass)
3. **SQLite per-tenant `.db` file** path resolution — entity sits on whichever DbContext is appropriate (catalog → per-tenant file; identity → master_SaaS.db); existing `SqlitePerTenantDbContextFactory` handles routing
4. **MariaDB** app-layer filter only (no native RLS); same `TenantAwareDbContextBase` mechanics apply; documented as posture-difference
5. **Cosmos DB** uses `TenantId` as partition key (`/tenantId`); cross-partition queries forbidden
6. **Single-tenant mode** support — when `IsMultiTenantMode=false`, query filter is a no-op but entity still has `TenantId` column for schema consistency; SaaS-as-itself deployments use a single fixed tenant id

**Why:** During the 2026-05-19 WC catalog walkthrough I had been listing new entities (ProductReview, CuratedCollection, ShoppableImage, LoyaltyTier, ReferralCode, PromoBanner, etc.) without explicitly calling out the isolation acceptance criteria. The user (correctly) flagged this as a risk because a new entity could ship without proper isolation and immediately become a cross-tenant data-leak vector.

**How to apply:**
- Every WC backend-addition proposal must inline a 6-line isolation block: ITenantOwned / RLS migration / SQLite DbContext placement / MariaDB / Cosmos partition / single-tenant mode
- When writing PLAN.md WC entries, the isolation block goes alongside the entity description
- Tests for new entities must include cross-tenant isolation regression tests (read attempt from Tenant B for Tenant A's row must fail at both app layer + DB layer where RLS exists)
- Pattern reference: `tests/PolarSharp.MultiTenant.EntityFrameworkCore.Tests/CrossTenantIsolationTests.cs` (existing) is the template
