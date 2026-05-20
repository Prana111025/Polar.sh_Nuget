# Storefront Cart + Checkout

This article documents the cart + checkout services that `PolarSharp.EcommerceStorefronts` ships in v1.4.0 (Phase 25 of the storefront work). It assumes the reader is comfortable with .NET DI, ASP.NET Core middleware, and the rest of the PolarSharp architecture. For a non-technical walkthrough, see the [companion Implementation Narrative](narratives/storefronts-cart-and-checkout-for-customers.md).

## Scope

Three storefront-core packages collaborate to ship Phase 25:

| Package | Role |
|---|---|
| `PolarSharp.EcommerceStorefronts.Abstractions` | Interfaces + DTOs: `IStorefrontCartService`, `IStorefrontCheckoutService`, `IStorefrontCustomerService`, `IStorefrontCartStore`, `IStorefrontCheckoutSessionStore`, `IStorefrontCustomerSource`, `IGuestSessionAccessor`. Lift-safe (no `PolarSharp.*` deps outside `BaseEntities`). |
| `PolarSharp.EcommerceStorefronts` | Concrete services: `DefaultStorefrontCartService`, `DefaultStorefrontCheckoutService`, `DefaultStorefrontCustomerService`, `StorefrontClient`, plus in-memory store defaults. |
| `PolarSharp.EcommerceStorefronts.GuestSessions` | Signed-cookie guest-session round-trip, the `GuestSessionMiddleware`, and the HTTP-backed `IGuestSessionAccessor`. |

All three are lift-safe per [Case Study 01](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/Case%20Studies/01-Lift-And-Shift-Architecture.md). The CI guard `scripts/verify-storefronts-no-polarsharp-deps.sh` rejects any forbidden `PolarSharp.*` reference outside the storefront-core family.

## DI wiring

The minimum wiring for a storefront-enabled ASP.NET Core host:

```csharp
builder.Services.AddSingleton<IStorefrontCatalogProvider>(/* your catalog provider */);
builder.Services.AddPolarStorefrontsCore();
builder.Services.AddPolarOrderProcessingPipeline();   // optional; checkout works without it (returns CheckoutFailed)
builder.Services.AddPolarGuestSessions();             // optional; for anonymous shopping

// in your Program.cs after UseRouting():
app.UsePolarGuestSessions();
```

`AddPolarStorefrontsCore()` registers `TryAdd`-style defaults — hosts override any service by registering their own implementation *before* the call. The catalog provider is the one mandatory dependency the host must supply (no default ships because storefronts without a catalog are meaningless).

## The cart service

`IStorefrontCartService` exposes seven mutations, each returning `StorefrontResult<Cart>`:

- `GetCurrentCartAsync` — creates an empty cart if none exists
- `AddToCartAsync` — adds a SKU at a given quantity
- `UpdateLineQuantityAsync` — changes the count on an existing line (zero = remove)
- `RemoveLineAsync` — explicit removal
- `ApplyDiscountCodeAsync` — records a code; never validates it (the checkout pipeline does)
- `RemoveDiscountAsync` — clears the recorded code
- `SetShippingAddressAsync` — attaches an address for tax + shipping quotation

### Server-as-source-of-truth fraud-prevention discipline

Per [Case Study 03](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/Case%20Studies/03-Embed-Anywhere-Web-Components.md), the cart service is the enforcement seam for layered fraud prevention. Every mutation:

1. **Re-validates unit prices against the catalog.** The cart never trusts a client-supplied price; `AddToCartAsync` and `UpdateLineQuantityAsync` call `IStorefrontCatalogProvider.GetProductAsync` and use the catalog's price as authoritative.
2. **Re-validates product availability.** Out-of-stock products yield `StorefrontConflictError`.
3. **Clamps quantities.** Zero or negative quantities are rejected with `StorefrontValidationError`. `StorefrontOptions.MaxCartLineItems` (default 100) and `StorefrontOptions.MaxCartTotalValueCents` (default $10,000) are hard caps that fail the mutation.
4. **Recomputes totals server-side on every mutation.** The returned `Cart.Totals` is freshly computed from the line items; the previous totals are discarded.
5. **Records discount codes without applying them.** The `ApplyDiscountsStage` in the order-processing pipeline validates the code against the merchant's discount table at checkout time. The cart service never grants a discount on the customer's word.

### Cart ownership (mode-agnostic)

Per [Case Study 05](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/Case%20Studies/05-Multi-Tenancy-As-Optional.md), the cart service resolves ownership without branching on tenancy mode:

| Identity state | Resolved owner | Tenant scope |
|---|---|---|
| Authenticated customer in single-tenant host | `CartOwner.FromCustomer(customerId)` | `None` |
| Authenticated customer in multi-tenant host | `CartOwner.FromCustomer(customerId)` | `Some(tenantId)` |
| Guest session in single-tenant host | `CartOwner.FromGuest(sessionId)` | `None` |
| Guest session in multi-tenant host | `CartOwner.FromGuest(sessionId)` | `Some(tenantId)` |
| No customer + no guest session | failure | — |

The `IStorefrontCartStore` partitions storage by `(owner, tenantId)` so the same customer can have distinct carts per tenant, and so a guest session never collides with an authenticated customer's cart.

## The checkout service

`IStorefrontCheckoutService` exposes three operations:

- `InitiateCheckoutAsync(InitiateCheckoutCommand, ct)` — snapshots the current cart into a persisted `CheckoutSession` in `CheckoutStatus.Initiated`
- `GetSessionAsync(Guid, ct)` — loads a session by id (used after a payment redirect)
- `ProcessCheckoutAsync(Guid, ct)` — runs the pipeline, yielding one `CheckoutPipelineEvent` per stage transition

`ProcessCheckoutAsync` returns `IAsyncEnumerable<CheckoutPipelineEvent>` so the storefront UI can stream progress (toast notifications, SignalR broadcasts) without polling. The pipeline emits:

- `CheckoutStageStarted` — at pipeline entry
- `CheckoutStageCompleted` — for every successful stage
- `CheckoutSucceeded` — terminal; carries the resulting `OrderId`
- `CheckoutFailed` — terminal; carries the `StorefrontError` describing the failure

The session is persisted to `IStorefrontCheckoutSessionStore` on every stage transition so subsequent `GetSessionAsync` calls reflect the latest state.

### Behaviour when the pipeline is not registered

`AddPolarOrderProcessingPipeline()` is *optional*. When it is not called, `ProcessCheckoutAsync` yields a single `CheckoutFailed` event with the reason key `Checkout.PipelineNotRegistered` so the storefront UI can render a coherent "checkout unavailable" surface rather than crash. This composes cleanly with the lift-shift contract: a host can install only the storefront-core packages and incrementally adopt the pipeline.

## The customer service

`IStorefrontCustomerService` gates every operation on `IStorefrontIdentityProvider.IsAuthenticated`. Guests receive `StorefrontAuthenticationError` for profile / orders / addresses calls. The wallet-balance method is the exception — it returns a zero-balance record (rather than an error) for guests so the account-area chrome can render uniformly without branching on auth state.

The service forwards to `IStorefrontCustomerSource` for the actual data; the storefront-core default is `NullStorefrontCustomerSource`, which returns `StorefrontNotFoundError` for read operations and no-ops for writes. Production hosts plug a real source via the `PolarSharp.EcommerceStorefronts.Polar.Reporting` bridge (ships in Phase 31).

## Guest sessions

`PolarSharp.EcommerceStorefronts.GuestSessions` adds anonymous-customer support via a signed cookie:

- `SignedCookieGuestSessionService` writes `id|createdTicks|expiresTicks` sealed by `IDataProtector.Protect`. The cookie is web-safe base64, `HttpOnly`, `Secure` when the request is HTTPS, `SameSite=Lax`.
- `GuestSessionMiddleware` resolves the session at the start of every request: read cookie → mint if absent → renew the expiry → attach to `HttpContext.Items` under `GuestSessionMiddleware.HttpContextItemKey`.
- `HttpContextGuestSessionAccessor` implements the storefront-core `IGuestSessionAccessor` by reading the resolved session off `HttpContext.Items`. `AddPolarGuestSessions()` `Replace()`s the storefront-core `NullGuestSessionAccessor` default so the cart service automatically picks up guest ownership.

Tampered cookies (signature failure, format mismatch, expired payload) are silently treated as "no session" so the middleware mints a fresh one — failure recovery without surfacing the breakage to the customer.

## Persistence in production

The storefront-core defaults are in-memory:

- `InMemoryStorefrontCartStore` — thread-safe; survives requests within a process; does NOT survive a restart.
- `InMemoryStorefrontCheckoutSessionStore` — same shape.

Production hosts replace both with EF Core / Redis-backed implementations registered before `AddPolarStorefrontsCore()`:

```csharp
builder.Services.AddSingleton<IStorefrontCartStore, EfStorefrontCartStore>();
builder.Services.AddSingleton<IStorefrontCheckoutSessionStore, RedisCheckoutSessionStore>();
builder.Services.AddPolarStorefrontsCore();
```

The lift-shift CI guard accepts these — the storefront-core abstractions deliberately have no dependency on EF Core or Redis, so the storage seam is the right place for the host to apply its persistence choice.

## See also

- [Implementation Narrative — Cart and Checkout, in plain language](narratives/storefronts-cart-and-checkout-for-customers.md)
- [Case Study 01 — Lift-and-Shift Architecture](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/Case%20Studies/01-Lift-And-Shift-Architecture.md)
- [Case Study 03 — Embed-Anywhere Web Components](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/Case%20Studies/03-Embed-Anywhere-Web-Components.md) — the 11-layer fraud-prevention model the cart service implements at the server boundary
- [Case Study 05 — Multi-Tenancy as Optional](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/Case%20Studies/05-Multi-Tenancy-As-Optional.md) — the mode-agnostic identity pattern the cart + checkout services follow
