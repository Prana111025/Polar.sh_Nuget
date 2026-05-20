# PolarSharp.PrepaidWallets — Lift-and-Shift Procedure

This document captures the complete strategy and mechanical steps for relocating the
PrepaidWallets feature out of the PolarSharp monorepo into its own standalone GitHub
repository at a future date — without rewriting any of the wallet code itself, and
without breaking any host that consumes PolarSharp's PrepaidWallets bridges.

> **Status**: Strategy authored 2026-05-15 alongside the v1.3.0 expansion plan. The
> lift-and-shift contract is the spine of the entire PrepaidWallets package
> decomposition; every package boundary, namespace, and dependency rule below was
> chosen specifically to make this lift mechanical, not architectural.

---

## The contract in one sentence

The `.Polar.` infix in the namespace `PolarSharp.PrepaidWallets.Polar.*` is the
entire lift boundary. Files with `.Polar.` in their namespace **stay** with PolarSharp
and get rewired post-lift; files without `.Polar.` (i.e. `PolarSharp.PrepaidWallets.*`
direct) **move** to the new repo via a single namespace rename.

---

## The two tiers

| Tier | Namespace pattern | Lift action | Allowed dependencies | Packages |
|---|---|---|---|---|
| **Wallet core** | `PolarSharp.PrepaidWallets.*` (no `.Polar.` infix anywhere in the name) | **Moves** to a new `PrepaidWallets/*` repo. Single namespace rename: `PolarSharp.PrepaidWallets` → `PrepaidWallets`. | MediatR, EF Core, Marten, HotChocolate, Scriban, Fluid, Microsoft.AspNetCore.Identity, Microsoft.AspNetCore.Authorization, Microsoft.AspNetCore.Components, Microsoft.AspNetCore.SignalR, Microsoft.Extensions.Localization.Abstractions, SendGrid, MailKit, Azure.Communication.Email, AWSSDK.SimpleEmailV2, Twilio, Stripe.net, PayPalCheckoutSdk, Microsoft.Extensions.*. **Zero** PolarSharp.* refs. | **28 packages** *(updated 2026-05-15 amendments 3 + 4 + 5 + 6)*: Abstractions, PrepaidWallets, EventStore.Marten, EventStore.EntityFrameworkCore (+ 5 provider variants: SqlServer/Sqlite/PostgreSQL/MariaDb/CosmosDb), Reporting, AspNetCore.Identity, AspNetCore.GraphQL, Notifications, Notifications.Email.SendGrid, Notifications.Email.MailKit, Notifications.Email.Azure, Notifications.Email.AwsSes, Notifications.Sms.Twilio, Notifications.Webhook, **Notifications.Templates.Scriban, Notifications.Templates.Fluid, Funding.Stripe, Funding.PayPal, AspNetCore.SignalR, Blazor.Customer, Blazor.Admin, SaaSInvoicing, PrefundedTenant** |
| **Polar bridges** | `PolarSharp.PrepaidWallets.Polar.*` | **Stays** with PolarSharp. After lift, `<ProjectReference>` swaps to `<PackageReference Include="PrepaidWallets.X">` against the new external NuGet feed. | Wallet core (or external lib post-lift) + PolarSharp internals (ICurrentUser, IPolarReportingClient, ICustomerTransactionContext, etc.) | **8 packages** *(updated 2026-05-15 amendments 1 + 3 + 5 + 6)*: Polar.Identity, Polar.Checkout, Polar.Reporting, Polar.GraphQL, Polar.PurchaseOrder, Polar.Notifications, **Polar.Translation, Polar.SaaSInvoicing** |

> **Schema scope of wallet core** *(2026-05-15 amendments 1 + 3)*: wallet core owns these 10 tables, all of which move with the lift:
>
> - `wallet_events` + `wallet_snapshots` (the original event-store tables)
> - `wallet_products` (snapshotted at debit-time from EcommerceStoreManagement; flat-variant shape with `parent_product_id` FK; SKU + axes denormalized)
> - `wallet_orders` + `wallet_order_lines` (cart snapshot per debit; carries `tokens_at_purchase` for audit immutability)
> - `wallet_purchase_orders` + `wallet_purchase_order_lines` (B2B PO entity with line items; status lifecycle: open → partial_credit → fully_credited → voided)
> - **`wallet_notifications`** (delivery audit + dead-letter store for the queued/retried dispatcher)
> - **`wallet_notification_preferences`** (per-recipient per-event-type per-channel opt-in/out)
> - **`wallet_notification_recipients`** (denormalized recipient contact info: email, phone, locale, webhook URLs)
>
> Bridges populate these tables. Wallet core never queries upstream. Post-lift, an external host populates them via whatever upstream system they use.

> **Localization scope of wallet core** *(2026-05-15 amendment 3)*: every wallet package commits to `IStringLocalizer<T>` for every user-facing string — validation errors, audit log descriptions, GraphQL errors, health-check status, notification message templates, reporting column headers + status strings. Each package ships `Resources/SR.resx` (en-US default) + `Resources/SR.es-MX.resx`. The `IStringLocalizer<T>` abstraction is in `Microsoft.Extensions.Localization.Abstractions` (Microsoft.* dep — lift-safe). Resource files move with the lift verbatim.

> **Notification dispatcher posture** *(2026-05-15 amendment 3)*: queued + retried via bounded `Channel<NotificationEnvelope>` per tenant + `WalletNotificationDispatcherService` IHostedService; same shape as the v1.1.0 webhook delivery and v1.3 graph projection daemon. Dead-letters to `wallet_notifications` after 3 retries (configurable). Bidirectional sender model: System / HostOperator / Tenant / Customer all dispatch through the same pipeline with appropriate authorization scoping. Multi-tenancy is OPTIONAL — single-tenant hosts use the same dispatcher with `WalletAudienceScope.HostOperator` covering both single-tenant operator AND multi-tenant SaaSAdmin authority.

> **SaaS payment model and Option D recursion** *(2026-05-15 amendment 6)*: the SaaS chooses ONE settlement mode platform-wide via `PolarSharp:SaaS:SettlementMode` — `StripeConnect` (A; Stripe-only platform), `BundledMonthlyInvoice` (B; all processors; bundled into tenant's existing SaaS subscription invoice), `StandalonePolarOrder` (C; all processors for tenant-customers; separate Polar Order for SaaS-to-tenant settlement), or `TenantPrefundedWallet` (D; meta-recursive — tenants are customers in the SaaS's meta-tenant wallet context; SaaS fees debit tenant wallets in real-time; progressive 5-stage balance escalation with auto-suspension of tenant admin UI when grace period expires). Per-tenant SaaS fee rates (`FundingCutPercent`, `NonPrepaidTransactionCutPercent`, `MaintenanceFeeCutPercent`, `RefundSurchargeCutPercent`) live on `TenantBusinessProfile.SaaSFeeRates` and are settable only by AppMasterAdmin via the NEW `PolarPermission.ManageTenantBilling`. The `IBalanceEscalationPolicy` framework is generic and reusable: defaults ship for both tenant-wallet (5-stage) and customer-wallet (3-stage) low-balance scenarios. All this lift-safe scope sits in `PolarSharp.PrepaidWallets.SaaSInvoicing` + `PolarSharp.PrepaidWallets.PrefundedTenant` (move with the lift); the Polar-specific settlement bridge lives in `PolarSharp.PrepaidWallets.Polar.SaaSInvoicing` (stays).

---

## The mechanical CI guard makes the boundary unbreakable

Phase 22 of the v1.3.0 plan ships
`scripts/verify-prepaidwallets-no-polarsharp-deps.sh`. It runs
`dotnet list package --include-transitive` against every wallet-core csproj and
**fails the build** if any `PolarSharp.*` (other than another wallet-core package)
appears in the dependency graph. CI runs it on every PR. A developer who accidentally
adds `using PolarSharp.BaseEntities;` inside a wallet-core file breaks the build.

The guard is the entire reason the lift remains feasible — without it, the boundary
erodes silently over time.

---

## The interface seams that let bridges plug in PolarSharp behavior

Three interfaces in `PolarSharp.PrepaidWallets.Abstractions` define the integration
points. The wallet core depends only on these interfaces; the `.Polar.*` bridge
packages provide PolarSharp-specific implementations.

| Interface | Wallet-core role | Bridge implementation (pre-lift) | Post-lift action |
|---|---|---|---|
| `IWalletIdentityProvider` | "Who is performing this op?" Wallet core calls it on every command. | `PolarSharp.PrepaidWallets.Polar.Identity` wires it via PolarSharp's `ICurrentUser` + `ICurrentTenant`. | Bridge package keeps the same impl; the wallet-core interface was lifted, the bridge is rewired to depend on the new external `PrepaidWallets.Abstractions` package. |
| `IWalletTransactionContext` | Optional IP / UA / session metadata for fraud detection on every event. | `PolarSharp.PrepaidWallets.Polar.Identity` adapts v1.3 `CustomerTransactionContext` from PolarSharp.BaseEntities into the wallet-core type. | Bridge package keeps adapting; new repo's interface is identical (lifted verbatim). |
| `ITokenExchangeRateResolver` | Currency → tokens-per-unit lookup. Default impl in wallet core; bridges can override per-tenant. | `PolarSharp.PrepaidWallets.Polar.Checkout` overrides per tenant via `TenantBusinessProfile`. | Bridge package overrides the same way; default impl in lifted wallet core still works. |

Because the wallet core defines its OWN `IWalletTransactionContext` type (no
PolarSharp.BaseEntities dep), the wallet core can ship to a new repo without dragging
PolarSharp dependencies. The bridge package does the type adaptation between
PolarSharp's `CustomerTransactionContext` and the wallet's `IWalletTransactionContext`.

---

## Funding processors: a special seam

`IWalletFundingProcessor` in `PolarSharp.PrepaidWallets.Abstractions` defines the
contract for "something that can accept a payment from a customer and report back
when the funds have cleared." Three concrete implementations ship with the wallet,
and the split across tiers matters for the lift:

- `PolarSharp.PrepaidWallets.Funding.Stripe` — **lift-safe**; Stripe Charges +
  optional Stripe Connect `application_fee_amount`. Moves with the wallet on lift.
- `PolarSharp.PrepaidWallets.Funding.PayPal` — **lift-safe**; PayPal Orders v2 +
  optional PayPal Payouts API. Moves with the wallet on lift.
- `PolarSharp.PrepaidWallets.Polar.Checkout` — **Polar bridge (stays)**; the
  `PolarFundingProcessor` inside this package is the primary on-ramp for the
  SaaS tenant prepayment flow. When tenants prepay for platform usage, this is
  the path their payment travels. It is also the on-ramp for the entire Option D
  `TenantPrefundedWallet` settlement mode.

This means that out of the three shipped processors, two move with the wallet on
lift (Funding.Stripe + Funding.PayPal) and one stays in PolarSharp
(Polar.Checkout). Post-lift, the Polar.Checkout bridge continues to consume the
lifted wallet via `PackageReference` and its `PolarFundingProcessor` continues to
fulfill the funding role for any PolarSharp host that uses it. Hosts wanting
processors beyond these three (Square, Adyen, Braintree, etc.) implement
`IWalletFundingProcessor` themselves against the wallet abstraction — no fork of
the wallet is required, and the implementation can live in either the host
codebase or in the post-lift wallet repo, depending on whether the host wants
the processor to be lift-safe.

For the end-to-end Polar.sh funding flow narrative, see
`src/PolarSharp.PrepaidWallets.Polar.Checkout/README.md` and Case Study 02's
"Funding flow: Polar.sh as the primary on-ramp" section.

---

## The 5-line lift script

When you decide to lift, here's exactly what happens:

```bash
# In a new PrepaidWallets repo:
git clone the-new-repo && cd the-new-repo
cp -r ../PolarSharp/src/PolarSharp.PrepaidWallets.{Abstractions,EventStore.*,Reporting,AspNetCore.*,Notifications*,Funding.*,Blazor.*,SaaSInvoicing,PrefundedTenant} ./src/
cp -r ../PolarSharp/src/PolarSharp.PrepaidWallets ./src/

# Strip the PolarSharp. prefix from directory names + namespaces
find ./src -type d -name "PolarSharp.PrepaidWallets*" -exec rename 's/PolarSharp\.PrepaidWallets/PrepaidWallets/' {} +
find ./src -type f \( -name "*.cs" -o -name "*.csproj" -o -name "*.md" \) -exec \
  sed -i 's/PolarSharp\.PrepaidWallets/PrepaidWallets/g; s/namespace PolarSharp\.PrepaidWallets/namespace PrepaidWallets/g' {} +

# Wire CI + publish to NuGet
```

> **2026-05-15 note (amendments 2 + 3)**: the lift now ALSO carries:
> - 2 lift-safe `AspNetCore.*` packages (`PolarSharp.PrepaidWallets.AspNetCore.Identity` + `PolarSharp.PrepaidWallets.AspNetCore.GraphQL`) — give single-tenant ASP.NET Core hosts a complete wallet integration without depending on PolarSharp.MultiTenant.*
> - 7 lift-safe `Notifications*` packages (`PolarSharp.PrepaidWallets.Notifications`, `Notifications.Email.SendGrid`, `Notifications.Email.MailKit`, `Notifications.Email.Azure`, `Notifications.Email.AwsSes`, `Notifications.Sms.Twilio`, `Notifications.Webhook`) — queued+retried multi-channel dispatcher with full IStringLocalizer support
>
> All 9 of these packages have ZERO PolarSharp.* dependencies and go with the wallet core lift. The bash globs `AspNetCore.*` and `Notifications*` above pick them up automatically.

That moves all 10 wallet-core packages to the new repo. Then **back in PolarSharp**,
the 4 `.Polar.*` bridge packages each bump their dependency:

```diff
- <ProjectReference Include="..\..\src\PolarSharp.PrepaidWallets.Abstractions\PolarSharp.PrepaidWallets.Abstractions.csproj" />
+ <PackageReference Include="PrepaidWallets.Abstractions" Version="1.0.0" />
```

…and rebuild + republish the 4 bridge packages.

**Public API surface unchanged for any host that consumed the bridges.** The host's
existing `using PolarSharp.PrepaidWallets.Polar.Checkout;` keeps working; the bridge
internally swapped what it depends on, but its own surface didn't change.

---

## Files that get touched in the lift

### Files that MOVE (28 packages × ~20 files each ≈ 560 files; updated 2026-05-15 amendments 2 + 3 + 4 + 5 + 6)

```
# Original wallet core (10 packages)
src/PolarSharp.PrepaidWallets.Abstractions/
src/PolarSharp.PrepaidWallets/
src/PolarSharp.PrepaidWallets.EventStore.Marten/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.SqlServer/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.Sqlite/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.PostgreSQL/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.MariaDb/
src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.CosmosDb/
src/PolarSharp.PrepaidWallets.Reporting/

# Single-tenant lift-safe bridges (amendment 2; 2 packages)
src/PolarSharp.PrepaidWallets.AspNetCore.Identity/
src/PolarSharp.PrepaidWallets.AspNetCore.GraphQL/

# Notification dispatcher + channels (amendment 3; 7 packages)
src/PolarSharp.PrepaidWallets.Notifications/
src/PolarSharp.PrepaidWallets.Notifications.Email.SendGrid/
src/PolarSharp.PrepaidWallets.Notifications.Email.MailKit/
src/PolarSharp.PrepaidWallets.Notifications.Email.Azure/
src/PolarSharp.PrepaidWallets.Notifications.Email.AwsSes/
src/PolarSharp.PrepaidWallets.Notifications.Sms.Twilio/
src/PolarSharp.PrepaidWallets.Notifications.Webhook/

# Template engines (amendment 4; 2 packages)
src/PolarSharp.PrepaidWallets.Notifications.Templates.Scriban/
src/PolarSharp.PrepaidWallets.Notifications.Templates.Fluid/

# Funding processors (amendment 5; 2 packages)
src/PolarSharp.PrepaidWallets.Funding.Stripe/
src/PolarSharp.PrepaidWallets.Funding.PayPal/

# Real-time UI push + Blazor RCLs (amendment 5; 3 packages)
src/PolarSharp.PrepaidWallets.AspNetCore.SignalR/
src/PolarSharp.PrepaidWallets.Blazor.Customer/
src/PolarSharp.PrepaidWallets.Blazor.Admin/

# SaaS payment story (amendment 6; 2 packages)
src/PolarSharp.PrepaidWallets.SaaSInvoicing/       # accrual ledger + monthly invoice composer (supports settlement modes A/B/C/D)
src/PolarSharp.PrepaidWallets.PrefundedTenant/     # Option D meta-recursive tenant-wallet + progressive balance escalation framework

tests/PolarSharp.PrepaidWallets.*.Tests/   ← entire test suite for wallet core (excludes the .Polar.* bridges below)
```

### Files that STAY (8 bridge packages; updated 2026-05-15 amendments 1 + 3 + 5 + 6)

```
src/PolarSharp.PrepaidWallets.Polar.Identity/
src/PolarSharp.PrepaidWallets.Polar.Checkout/
src/PolarSharp.PrepaidWallets.Polar.Reporting/
src/PolarSharp.PrepaidWallets.Polar.GraphQL/
src/PolarSharp.PrepaidWallets.Polar.PurchaseOrder/      # amendment 1: B2B PO admin
src/PolarSharp.PrepaidWallets.Polar.Notifications/      # amendment 3: per-tenant BYOK + Identity-aware recipient resolver
src/PolarSharp.PrepaidWallets.Polar.Translation/        # amendment 5: bridges wallet template translation to PolarSharp's 3-tier translation resolver
src/PolarSharp.PrepaidWallets.Polar.SaaSInvoicing/      # amendment 6: Polar-specific settlement delivery for modes B and C

tests/PolarSharp.PrepaidWallets.Polar.*.Tests/
```

### Files in PolarSharp that need 1-line edits per bridge package

For each of the 8 bridge csproj files: replace ProjectReference → PackageReference
(see diff above). That's 8 csproj edits, 1 line each.

### Files that are NEW in PolarSharp post-lift

A small "PolarSharp wires the external PrepaidWallets library" composition extension
in each bridge — but the bridge code was already authored to consume the wallet-core
interfaces, so the only change is the dependency type. No new files.

---

## Verification after the lift

These checks pass before merging the lift PR:

1. **PolarSharp CI green** — bridges still compile against `PrepaidWallets.*` package refs.
2. **Wallet-core CI green in new repo** — same test suite, just renamed.
3. **A representative host project** — the integration test that exercises the full
   wallet checkout flow should run unchanged. Same `using` statements, same DI wiring.
4. **`dotnet list package --include-transitive`** on each bridge package shows
   `PrepaidWallets.*` deps (the new external lib) where it previously showed
   `PolarSharp.PrepaidWallets.*` (project refs). No PolarSharp internals leak into
   wallet core in either repo.
5. **NuGet feed** — the new `PrepaidWallets.*` packages publish to nuget.org (or your
   chosen feed) with version 1.0.0, matching the version you cut PolarSharp at on lift day.

---

## Why this works

The bridges hold ALL the PolarSharp coupling. Wallet core never knows PolarSharp
exists. So the lift is mechanical, not architectural.

The CI guard prevents drift. The interface seams provide the integration points.
The namespace separator makes the boundary visible to any developer.

The lift becomes a 30-minute PR, not a multi-week refactor.

---

## When NOT to lift

Don't lift if:

- The wallet feature gets minor adoption — keeping it inside PolarSharp keeps the
  release cadence simpler (one CI, one set of versioned packages).
- The wallet core needs to start depending on PolarSharp internals for some reason
  (unlikely given the design, but possible if scope expands significantly).
- The lift would split a release boundary mid-work (wait for v1.3.x to settle first).

Lift WHEN:

- A second host product (outside PolarSharp) wants to use the wallet — the strongest
  signal that wallet core deserves its own SemVer cycle.
- Wallet-specific feature requests start outpacing PolarSharp's release cadence.
- A separate team takes ownership of wallets.

---

## See also

- `/Users/mollsandhersh/.claude/plans/we-will-be-building-stateless-pretzel.md` —
  the v1.3.0 expansion plan, "Prepaid wallets — design detail (lift-and-shift
  architecture)" section for the full design rationale and package decomposition.
- The 14 PrepaidWallets package csproj files (scaffolded in v1.3.0 Phase 12,
  commit `0bb3075`) — already structured per this contract.
- `scripts/verify-prepaidwallets-no-polarsharp-deps.sh` (will land in Phase 22) —
  the CI guard that enforces the contract.
