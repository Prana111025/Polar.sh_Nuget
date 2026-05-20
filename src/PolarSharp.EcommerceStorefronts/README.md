# PolarSharp.EcommerceStorefronts

> Core storefront orchestration: `IStorefrontClient` + cart + checkout + customer services. Lift-safe (zero `PolarSharp.*` deps outside `BaseEntities` + the storefront-core family).

## Status

v1.4.0 — Phase 25 ships cart + checkout + customer services with in-memory persistence defaults and the `AddPolarStorefrontsCore()` DI extension.

## Install

```sh
dotnet add package PolarSharp.EcommerceStorefronts
```

## Quickstart

```csharp
// In Program.cs (or your DI composition root):
builder.Services.AddSingleton<IStorefrontCatalogProvider>(/* your catalog provider */);
builder.Services.AddPolarStorefrontsCore(opts =>
{
    opts.MaxCartLineItems = 100;
    opts.MaxCartTotalValueCents = 1_000_000;   // $10,000
});

// Optional: order-processing pipeline + guest sessions.
builder.Services.AddPolarOrderProcessingPipeline();
builder.Services.AddPolarGuestSessions();

// In your endpoints / Razor pages:
public sealed class CartEndpoints(IStorefrontClient storefront)
{
    public async Task<Cart> Get(CancellationToken ct)
    {
        var result = await storefront.Cart.GetCurrentCartAsync(ct);
        return result.Match(c => c, e => throw new Exception(e.Message));
    }
}
```

## What this package gives you

- `DefaultStorefrontCartService` — server-side cart mutations with built-in fraud-prevention discipline (catalog re-validation, quantity clamping, totals recomputation on every change).
- `DefaultStorefrontCheckoutService` — converts a cart into a `CheckoutSession`, then runs the order-processing pipeline as an `IAsyncEnumerable<CheckoutPipelineEvent>`.
- `DefaultStorefrontCustomerService` — customer self-service (profile, orders, addresses, wallet balance) gated on `IStorefrontIdentityProvider`.
- `InMemoryStorefrontCartStore` + `InMemoryStorefrontCheckoutSessionStore` — in-process persistence defaults (replace in production).
- `IStorefrontClient` — ambient facade exposing all four services under one DI registration.
- `StorefrontOptions` — tunables (cart limits, idempotency header name, guest session lifetime).

For the full design narrative see [docs/articles/storefronts-cart-checkout.md](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/docs/articles/storefronts-cart-checkout.md) and the friendlier [Implementation Narrative](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/docs/articles/narratives/storefronts-cart-and-checkout-for-customers.md).
