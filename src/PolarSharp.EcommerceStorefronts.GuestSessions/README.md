# PolarSharp.EcommerceStorefronts.GuestSessions

> Anonymous-customer cart persistence via signed cookies. Adds the `GuestSessionMiddleware` + an HTTP-backed `IGuestSessionAccessor` so storefront-core services can transparently work for both guests and signed-in customers.

## Status

v1.4.0 — Phase 25 ships the signed-cookie round-trip (create / read / renew / destroy) plus the middleware + accessor.

## Install

```sh
dotnet add package PolarSharp.EcommerceStorefronts.GuestSessions
```

## Quickstart

```csharp
// In Program.cs:
builder.Services.AddPolarStorefrontsCore();
builder.Services.AddPolarGuestSessions();

var app = builder.Build();

app.UseRouting();
app.UsePolarGuestSessions();   // resolves + renews the guest session on every request
app.UseAuthorization();
app.MapRazorPages();
```

## What this package gives you

- `SignedCookieGuestSessionService` — reads + writes the `polar_guest_session` cookie. The payload is a compact `id|createdTicks|expiresTicks` triple sealed by ASP.NET Core's `IDataProtector` (HttpOnly, Secure-when-HTTPS, SameSite=Lax).
- `GuestSessionMiddleware` — at the start of every request, reads the existing session (or mints one), renews the expiry, and attaches the session to `HttpContext.Items` under `GuestSessionMiddleware.HttpContextItemKey`.
- `HttpContextGuestSessionAccessor` — implements the storefront-core `IGuestSessionAccessor` by reading off `HttpContext.Items`. `AddPolarGuestSessions()` `Replace()`s the storefront-core `NullGuestSessionAccessor` default so the cart service automatically picks up guest ownership.
- `AddPolarGuestSessions()` — DI extension that registers all three plus the required `IHttpContextAccessor` + `AddDataProtection()` wiring. Idempotent: hosts that already configured data-protection elsewhere are not disrupted.

## Data-protection key management

For multi-server deployments (load-balanced apps, container farms), configure ASP.NET Core's data-protection with a shared key store (Azure Key Vault, AWS KMS, a shared file share, etc.) so cookies issued by one server are readable by every other. PolarSharp does NOT manage keys — that is the host's responsibility.

See [docs/articles/storefronts-cart-checkout.md](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/docs/articles/storefronts-cart-checkout.md#guest-sessions) for the full picture.
