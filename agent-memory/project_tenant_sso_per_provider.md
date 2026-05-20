---
name: Per-tenant SSO architecture (BYOK + per-provider packages)
description: PolarSharp tenant marketplaces support per-tenant configured SSO via per-provider packages (Google, Microsoft, Facebook, Apple, etc.) with BYOK credentials + real-time validation
type: project
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
User design direction 2026-05-19: PolarSharp supports per-tenant social SSO across major providers (Google, Microsoft, Facebook, Apple, GitHub, LinkedIn, X, Snapchat, Pinterest, TikTok). Architecture mirrors the v1.3 translation-provider + tenant-AI patterns:

**Per-provider packages** (`PolarSharp.MultiTenant.Identity.Sso.*`):
- Google, Microsoft (Identity Platform), Facebook, Apple, GitHub, LinkedIn, X, Snapchat, Pinterest, TikTok
- YouTube uses Google SSO (same provider, same credentials, different scopes)
- Instagram uses Facebook SSO (Meta-owned)
- Each package wraps the corresponding `Microsoft.AspNetCore.Authentication.{Provider}` package (where one exists) or implements custom OAuth/OIDC for niche providers

**Per-tenant BYOK with real-time validation** (NOT SaaS-master fallback; same policy as tenant-AI):
- Tenant registers OAuth app with each provider; enters client_id + client_secret in tenant admin
- New entity `TenantSsoProvider` (per tenant + per provider; standard 5-layer isolation; credentials encrypted via IPolarSecretProtector)
- On credential save: real-time test against provider's discovery endpoint / token-introspection
- Customer-facing SSO button only appears when validated

**New WC** (v1.4.0 candidate): `polar-sso-button-group` — renders configured providers' sign-in buttons; composes with polar-checkout-page (auth step) and polar-account-menu (sign-in flow)

**Account-merging**: same email across SSO providers + password auth = same account by default. First successful auth claims the account. Subsequent auth methods link with confirmation prompt.

**Relation to existing KeyCloak (v1.2.x)**: KeyCloak is for ENTERPRISE SSO (SaaS host's staff signs into their own admin); social SSO is for CUSTOMER-FACING auth on the tenant's marketplace. Different audiences; both can coexist.

**v1.4.0 scope question pending**: which subset of providers in v1.4.0 launch vs defer. My recommendation: top 4 (Google + Microsoft + Facebook + Apple) for v1.4.0; rest in v1.4.x patches.
