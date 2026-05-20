# PolarSharp.EcommerceStorefronts.Abstractions

> Interfaces + DTOs for the PolarSharp EcommerceStorefronts feature — `IStorefrontIdentityProvider`, `IStorefrontCatalogProvider`, `IStorefrontCartService`, `IStorefrontCheckoutService`, `IStorefrontCustomerService`, `IStorefrontSearchProvider`, `IStorefrontShippingProvider`, `IStorefrontTaxProvider`, and the storefront-core DTO library.

## Status

v1.4.0 — Phase 25 adds the cart + checkout + customer-source + guest-session-accessor seams alongside the existing theming (`PolarThemeTokens`) and Web Component catalog (`WcCatalog`) registries.

## Install

```sh
dotnet add package PolarSharp.EcommerceStorefronts.Abstractions
```

## What's in here

- **Service interfaces** for every storefront-core operation. Implementations live in `PolarSharp.EcommerceStorefronts` (defaults) or in `PolarSharp.EcommerceStorefronts.Polar.*` bridges (Polar.sh-aware overrides).
- **Lift-safe DTOs** — `Cart`, `CartLineItem`, `CartTotals`, `CheckoutSession`, `CheckoutPipelineEvent`, `CustomerProfile`, `OrderDetail`, `ShippingAddress`, `WalletBalance`, etc.
- **`StorefrontResult<TValue>` + `StorefrontError`** — the result/error envelope every service returns. Bridges translate provider-specific errors into one of the six concrete `StorefrontError` sub-records.
- **`StorefrontOption<T>`** — lift-safe optional-value carrier (mirrors the `Option<T>` in `PolarSharp.PrepaidWallets.Abstractions` so wallets + storefronts share the same shape without sharing assemblies).
- **`PolarThemeTokens`** — the code-as-source-of-truth registry of CSS Custom Property tokens that storefront themes + Web Components consume.
- **`WcCatalog`** — the agent-readable catalog of every Web Component PolarSharp ships across v1.4.0 + v1.4.x + v1.5+.
- **Pipeline abstractions** — `IOrderProcessingStage`, `OrderInProcess`, `PipelineOutcome`, `PipelineStageContext` (consumed by `PolarSharp.EcommerceStorefronts.Pipelines.*`).

The package has ZERO `PolarSharp.*` dependencies outside `PolarSharp.BaseEntities` — it is the lift-safe boundary of the storefront feature family.
