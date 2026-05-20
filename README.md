# PolarSharp

> A .NET 10 Native AOT-compatible SDK for [Polar.sh](https://polar.sh) — the open-source Merchant of Record payment and monetization platform.

> **v1.2.0+** — what started as a Polar.sh SDK has grown into a full multi-tenant SaaS toolkit. **31 packages** cover everything from the raw API client to programmatic merchant onboarding, SQL-backed tenant + identity stores with row-level security, a local catalog with AI-translated product copy, hierarchical reporting, KeyCloak SSO, and bulk fake-data seeding. Pick the pieces you need; the v1.1.0 core SDK keeps working unchanged for hosts that don't.

[![CI](https://github.com/MollsAndHersh/Polar.sh_Nuget/actions/workflows/ci.yml/badge.svg)](https://github.com/MollsAndHersh/Polar.sh_Nuget/actions/workflows/ci.yml)
[![Publish Docs](https://github.com/MollsAndHersh/Polar.sh_Nuget/actions/workflows/docs.yml/badge.svg)](https://github.com/MollsAndHersh/Polar.sh_Nuget/actions/workflows/docs.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Docs](https://img.shields.io/badge/docs-github.io-informational)](https://mollsandhersh.github.io/Polar.sh_Nuget/)
[![Latest release](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=latest&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/releases)

> **31 packages distributed via [GitHub Packages](https://github.com/mollsandhersh/Polar.sh_Nuget/packages).** See [Installing from GitHub Packages](#installing-from-github-packages) below. All packages release together under one repo tag — every version badge below reflects the latest tagged release.

<details>
<summary><strong>All 31 packages</strong> — click to expand version badges (each links to its GitHub Packages page)</summary>

**Core (5)**

[![PolarSharp](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp)
[![PolarSharp.Webhooks](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.Webhooks&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.Webhooks)
[![PolarSharp.MultiTenant](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.MultiTenant&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.MultiTenant)
[![PolarSharp.Templates](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.Templates&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.Templates)
[![PolarSharp.BaseEntities](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.BaseEntities&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.BaseEntities)

**Multi-tenant SQL store (4)**

[![PolarSharp.MultiTenant.EntityFrameworkCore](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.MultiTenant.EntityFrameworkCore&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.MultiTenant.EntityFrameworkCore)
[![PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer)
[![PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite)
[![PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL)

**Identity + SSO (5)**

[![PolarSharp.MultiTenant.Identity](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.MultiTenant.Identity&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.MultiTenant.Identity)
[![PolarSharp.MultiTenant.Identity.SqlServer](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.MultiTenant.Identity.SqlServer&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.MultiTenant.Identity.SqlServer)
[![PolarSharp.MultiTenant.Identity.Sqlite](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.MultiTenant.Identity.Sqlite&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.MultiTenant.Identity.Sqlite)
[![PolarSharp.MultiTenant.Identity.PostgreSQL](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.MultiTenant.Identity.PostgreSQL&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.MultiTenant.Identity.PostgreSQL)
[![PolarSharp.MultiTenant.Identity.KeyCloak](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.MultiTenant.Identity.KeyCloak&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.MultiTenant.Identity.KeyCloak)

**Onboarding (1)**

[![PolarSharp.Onboarding](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.Onboarding&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.Onboarding)

**Ecommerce store management + AI translation (10)**

[![PolarSharp.EcommerceStoreManagement](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.EcommerceStoreManagement&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.EcommerceStoreManagement)
[![PolarSharp.EcommerceStoreManagement.EntityFrameworkCore](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.EcommerceStoreManagement.EntityFrameworkCore&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore)
[![PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer)
[![PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Sqlite](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Sqlite&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Sqlite)
[![PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL)
[![PolarSharp.EcommerceStoreManagement.Translation.Anthropic](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.EcommerceStoreManagement.Translation.Anthropic&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.EcommerceStoreManagement.Translation.Anthropic)
[![PolarSharp.EcommerceStoreManagement.Translation.OpenAI](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.EcommerceStoreManagement.Translation.OpenAI&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.EcommerceStoreManagement.Translation.OpenAI)
[![PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI)
[![PolarSharp.EcommerceStoreManagement.Translation.Gemini](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.EcommerceStoreManagement.Translation.Gemini&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.EcommerceStoreManagement.Translation.Gemini)
[![PolarSharp.EcommerceStoreManagement.Translation.Grok](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.EcommerceStoreManagement.Translation.Grok&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.EcommerceStoreManagement.Translation.Grok)

**Reporting (5)**

[![PolarSharp.Reporting](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.Reporting&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.Reporting)
[![PolarSharp.Reporting.EntityFrameworkCore](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.Reporting.EntityFrameworkCore&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.Reporting.EntityFrameworkCore)
[![PolarSharp.Reporting.EntityFrameworkCore.SqlServer](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.Reporting.EntityFrameworkCore.SqlServer&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.Reporting.EntityFrameworkCore.SqlServer)
[![PolarSharp.Reporting.EntityFrameworkCore.Sqlite](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.Reporting.EntityFrameworkCore.Sqlite&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.Reporting.EntityFrameworkCore.Sqlite)
[![PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL)

**Data seeding (1)**

[![PolarSharp.DataSeeding](https://img.shields.io/github/v/tag/MollsAndHersh/Polar.sh_Nuget?label=PolarSharp.DataSeeding&color=blue&sort=semver)](https://github.com/MollsAndHersh/Polar.sh_Nuget/pkgs/nuget/PolarSharp.DataSeeding)

</details>

---

## The Problem Nobody Talks About

You've chosen [Polar.sh](https://polar.sh) as your Merchant of Record. Smart move — they handle VAT, tax, compliance, and all the stuff that keeps accountants employed. You fire up your .NET project, head to the docs, and discover the official SDKs. Python? ✅ JavaScript? ✅ .NET? ...🦗

So you do what .NET developers do: you write it yourself. An `HttpClient`. Some DTOs. A rough stab at HMAC webhook verification. Retry logic that kind of works. A multi-tenant setup you're not entirely confident in. Six weeks later you have something that mostly works, except for that one edge case where a webhook gets processed twice and charges a customer twice. Whoops.

**PolarSharp exists so you never have to write any of that.**

---

## What You Get

**31 NuGet packages**, every one of them AOT-safe and ZH-style-compliant, grouped into seven capability areas. Install the ones you need; the rest stay out of your dependency graph.

| Capability area | Packages | What it does |
|---|---|---|
| **Core SDK + webhooks + client isolation** | `PolarSharp`, `PolarSharp.Webhooks`, `PolarSharp.MultiTenant`, `PolarSharp.Templates` | The v1.1.0 origin story. Full Polar.sh API client, HMAC-verified event handling, per-tenant `HttpClient` isolation with independent circuit breakers, `dotnet new` handler scaffolds. |
| **Universal domain model** | `PolarSharp.BaseEntities` | 15 abstract record bases that mirror Polar's webhook wire format byte-for-byte (Order, Customer, Product, Subscription, etc.) + 6 host-additive bases (Cart, Category, Department, Inventory, Sale). Inherit them in your own types and webhook payloads stop needing translation. |
| **SQL-backed tenant store** | `PolarSharp.MultiTenant.EntityFrameworkCore` + `.SqlServer` / `.Sqlite` / `.PostgreSQL` | When `appsettings.json` isn't enough. SQL Server + PostgreSQL get row-level security with an AppMasterAdmin bypass; SQLite gets one `.db` file per tenant for physical isolation. EF Core query filters + RLS = defense in depth. |
| **Identity + SSO** | `PolarSharp.MultiTenant.Identity` + `.SqlServer` / `.Sqlite` / `.PostgreSQL` + `.KeyCloak` | ASP.NET Core IdentityFramework with Guid IDs, M:N user↔tenant memberships, a site-level `AppMasterAdmin` tier orthogonal to tenant scope, five-layer cross-tenant safeguards, optional KeyCloak SSO via OIDC. |
| **Tenant onboarding** | `PolarSharp.Onboarding` | Single-call programmatic API (POST `/v1/organizations/` + OAT + webhook endpoint) **and** a resumable wizard with persistent sessions for interactive UI flows. Encrypts in-flight translation API keys at rest. Auto-provisions a TenantAdmin on completion. |
| **Local catalog + AI translation** | `PolarSharp.EcommerceStoreManagement` (+ EF Core + 3 providers) + `.Translation.Anthropic` / `.OpenAI` / `.AzureOpenAI` / `.Gemini` / `.Grok` | Author products, variants, categories, tier groups, discounts, and checkout links locally; publish to Polar idempotently with variant + tier expansion. Three-tier translation provider resolution (per-tenant BYOK → master → disabled). Refund service, license validator, inventory sync, admin audit log. |
| **Reporting** | `PolarSharp.Reporting` (+ EF Core + 3 providers) | Aggregate KPI reports (revenue, MRR, churn, fulfillment latency) **plus** lazy three-level hierarchical drilldown — Customers → Orders → Order details. Pre-aggregated columns keep a 10k-customer top-level grid loading in <100ms. Optional snapshot service mirrors Polar to local SQL on a schedule. |
| **Fake data for sandbox / QA** | `PolarSharp.DataSeeding` | Bogus-backed bulk generators at Demo / QA / Stress scales. Every fake record is tagged `IsFakeData=true` and filtered by a global EF query predicate; flip a per-tenant toggle to sync fake data to/from Polar's sandbox. |

---

## Features That Actually Matter

### It handles the boring stuff automatically

- **Idempotency keys** on every mutating request, stable across retries — no accidental double-charges when your circuit breaker kicks in
- **Retry + circuit breaker + timeout** via `Microsoft.Extensions.Http.Resilience` — Polar having a bad moment won't take your whole app down with it
- **`Polar-Version` header pinning** — date-lock your API contract so Polar's schema evolution doesn't silently break your production app
- **Token hot-reload** via `IOptionsMonitor` — rotate your access token by updating config; zero app restart required
- **Test/Live mode banner** at startup with a token-prefix sanity check, because accidentally charging real customers with a sandbox token (or vice versa) is a very bad day

### The error handling is actually ergonomic

No exceptions for recoverable HTTP errors. Every resource method returns `Result<TValue, PolarError>`:

```csharp
app.MapGet("/orders/{id}", async (string id, PolarClient polar, CancellationToken ct) =>
    (await polar.Orders.EmptyPathSegment[id].GetAsync(cancellationToken: ct))
        .Match(
            onSuccess: order => Results.Ok(order),
            onFailure: error  => error.ToHttpResult()));  // maps 401/403/404/422/429 correctly
```

No try-catch. No `if (response.IsSuccessStatusCode)`. Just a clean result you pattern-match against.

### Webhooks that don't keep you up at night

```csharp
// Register
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarWebhooks()
    .AddWebhookHandler<OrderCreatedEvent, OrderCreatedHandler>();

// Implement
public sealed class OrderCreatedHandler : PolarWebhookHandlerBase<OrderCreatedEvent>
{
    protected override Task HandleCoreAsync(OrderCreatedEvent @event, CancellationToken ct)
        => _orders.FulfillAsync(OrderId.From(@event.Data.Id), ct);
}
```

Polar delivers webhooks **at-least-once**. PolarSharp verifies every HMAC signature (timing-uniform — no oracle attacks), validates timestamps against replay attacks, exposes the `WebhookId` for your idempotency check, and lets you swap in a background queue (`enqueue: true`) for handlers that need more than 30 seconds to complete. It also warns you at startup if you forgot to register a handler for a known event type, because finding out at 2am that subscription cancellations have been silently discarded for three weeks is not fun.

### Multi-tenancy that actually isolates tenants

One misbehaving tenant — hammering Polar with bad requests, tripping a circuit breaker — must not freeze out all your other tenants. Each tenant gets its own `HttpClient`, its own connection pool, its own circuit breaker state, and its own rate limiter budget. When Tenant A's breaker opens, Tenant B continues serving requests without even noticing.

### Native AOT — for real

Zero reflection in hot paths. Source-generated JSON. Static event type lists (no `Assembly.GetTypes()`). Explicit `IValidateOptions<T>` instead of `ValidateDataAnnotations()`. CI gates `dotnet publish -p:PublishAot=true` with zero warnings on every PR. If you're deploying to Azure Container Apps, AWS Lambda, or anything else where cold-start time and binary size matter, PolarSharp won't be the thing that breaks your AOT build.

> **Developer note — AOT publish with `PolarSharp.DataSeeding`:** the bundled `PolarTestApp` does **not** transitively reference `PolarSharp.DataSeeding`, so `dotnet publish -p:PublishAot=true` against the test app stays clean. The `Bogus` faker library used by `PolarSharp.DataSeeding` does some reflection internally — hosts who publish AOT with `PolarSharp.DataSeeding` installed may see reflection / trim warnings. Two supported mitigations: (1) suppress the warnings only in the host's csproj via `<TrimmerRootAssembly Include="Bogus" />`, or (2) gate the `AddPolarDataSeeding(...)` registration behind `#if DEBUG` so the package compiles out of the Production build entirely. `PolarSharp.DataSeeding` is a dev-time package — designed for sandbox / QA / demo environments, not production hot paths — so either approach is acceptable.

### Observability without a config tax

```csharp
// That's it. This is the entire observability setup.
builder.Services.AddPolarInfrastructure(builder.Configuration);
```

Every API call emits an `ActivitySource("PolarSharp")` span, compatible with any OpenTelemetry backend. Every operation increments `Meter("PolarSharp")` counters and histograms. An `IHealthCheck` (tag: `"polar"`) shows up in your `/health` endpoint. Structured `ILogger.BeginScope` scopes attach `polar.request_id`, `polar.tenant_id`, `polar.resource`, and `polar.operation` to every log entry — with automatic PII redaction for customer emails and names.

---

## Installing from GitHub Packages

PolarSharp is distributed via [GitHub Packages](https://github.com/mollsandhersh/Polar.sh_Nuget/packages), not NuGet.org. Add the feed to your project's `NuGet.config` first (create this file at your solution root if it doesn't exist):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="PolarSharp" value="https://nuget.pkg.github.com/mollsandhersh/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <PolarSharp>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_GITHUB_PAT" />
    </PolarSharp>
  </packageSourceCredentials>
</configuration>
```

The `YOUR_GITHUB_PAT` is a [GitHub Personal Access Token](https://github.com/settings/tokens) with the `read:packages` scope — a read-only token is sufficient and safe to commit to CI secrets. Then install the packages you need.

**Core SDK** (the only required package):

```bash
dotnet add package PolarSharp
```

**Common opt-ins** for any host that handles webhooks or runs more than one tenant:

```bash
dotnet add package PolarSharp.Webhooks      # HMAC verification, toast notifications, background queues, reconciliation
dotnet add package PolarSharp.MultiTenant   # per-tenant HttpClient isolation with Finbuckle
dotnet new install PolarSharp.Templates     # dotnet new templates for scaffolding webhook handlers
dotnet add package PolarSharp.BaseEntities  # universal domain bases — Order, Customer, Product, etc.
```

**SQL-backed tenant + identity stores** (pick the provider that matches your DB):

```bash
dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer       # or .Sqlite / .PostgreSQL
dotnet add package PolarSharp.MultiTenant.Identity.SqlServer                  # or .Sqlite / .PostgreSQL
dotnet add package PolarSharp.MultiTenant.Identity.KeyCloak                   # optional OIDC SSO add-on
```

**Tenant onboarding and local catalog**:

```bash
dotnet add package PolarSharp.Onboarding
dotnet add package PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer    # or .Sqlite / .PostgreSQL
dotnet add package PolarSharp.EcommerceStoreManagement.Translation.Anthropic            # or .OpenAI / .AzureOpenAI / .Gemini / .Grok
```

**Reporting and data seeding**:

```bash
dotnet add package PolarSharp.Reporting.EntityFrameworkCore.SqlServer    # or .Sqlite / .PostgreSQL
dotnet add package PolarSharp.DataSeeding                                # dev-time only; see AOT note below
```

The full list of 31 packages is in the badge block at the top of this README. Every package depends transitively on the ones it needs — installing a provider package brings in its EF Core base and PolarSharp itself automatically.

### Minimum configuration

```json
{
  "PolarSharp": {
    "Mode": "Test",
    "AccessToken": "tok_sandbox_xxx"
  }
}
```

```csharp
// Program.cs — single call wires everything
builder.Services.AddPolarInfrastructure(builder.Configuration);

app.UsePolarInfrastructure();
```

### Full stack with webhooks and multi-tenancy

```csharp
builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarWebhooks()
    .AddWebhookHandler<OrderCreatedEvent,       OrderCreatedHandler>()
    .AddWebhookHandler<SubscriptionActiveEvent, SubscriptionHandler>()
    .AddPolarToastNotifications()
    .AddPolarMultiTenant();

app.UsePolarInfrastructure();
```

### Full configuration reference

> **For the v1.2.0+ packages** (Identity, Onboarding, EcommerceStoreManagement, Reporting, DataSeeding, KeyCloak, SQL tenant store, tenant cache, translation cache), see the [Configuration article](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/configuration.html) — every setting is documented there with valid values, defaults, and per-module `IValidateOptions<>` enforcement rules. The snippet below covers the v1.1.0 core sections (mode, access token, resilience, connection pool, webhooks, multi-tenant strategy).

```json
{
  "PolarSharp": {
    "Mode": "Test",
    "AccessToken": "tok_sandbox_xxx",
    "ApiVersion": "2025-01-15",
    "ApiVersionStrictness": "Warn",
    "TimeoutMs": 30000,
    "MaxRetries": 3,
    "Resilience": {
      "CircuitBreakerFailureThreshold": 5,
      "CircuitBreakerSamplingSeconds": 30,
      "CircuitBreakerBreakSeconds": 15,
      "HedgeAfterMs": null
    },
    "Connection": {
      "MaxConnectionsPerServer": 100,
      "PooledConnectionLifetimeMinutes": 15,
      "EnableHttp2": true,
      "EnableHttp3": false
    },
    "Webhooks": {
      "Secrets": ["whsec_xxx"],
      "Path": "/hooks/polar",
      "RequireHttps": true,
      "ToleranceSeconds": 300,
      "ToastNotifications": {
        "Enabled": true,
        "Events": [
          {
            "EventType": "order.created",
            "Title": "New Order",
            "MessageTemplate": "Order #{OrderNumber} from {CustomerEmail}",
            "Severity": "Success",
            "DurationSeconds": 5
          }
        ]
      }
    },
    "MultiTenant": {
      "Strategy": "Header",
      "Header": { "Name": "X-Tenant-ID" },
      "Tenants": [
        {
          "Id": "acme",
          "Identifier": "acme",
          "PolarAccessToken": "tok_live_acme",
          "Server": "Production"
        }
      ]
    }
  }
}
```

---

## Scaffold a Webhook Handler in One Command

```bash
dotnet new install PolarSharp.Templates

dotnet new polar-handler --event OrderCreatedEvent --name OrderCreatedHandler
```

Generates a complete, XML-documented, compilable handler class with all available event data properties listed in the doc comments. Covers all 28 Polar event types.

---

## Cloning to a fresh machine

This repo carries everything needed to bootstrap a fresh-machine clone with full agent + memory context. Two install scripts handle the off-project state that `git clone` doesn't replicate by default.

```sh
# 1. Clone the repo wherever you want it
git clone https://github.com/MollsAndHersh/Polar.sh_Nuget.git
cd Polar.sh_Nuget

# 2. Install home-rooted Claude agent files (AGENTS.md, CLAUDE.md, ZoranHorvat.md)
#    These apply to ALL Claude projects on the machine — not just PolarSharp.
./agent-context/install.sh

# 3. Install per-project Claude memory notes (architectural anchors + behavioral rules
#    accumulated across sessions). Without this, fresh-machine sessions start with no
#    memory context.
./agent-memory/install.sh
```

Both scripts:
- Back up any existing files at the destinations to a timestamped `$HOME/.agent-*-backup-YYYYMMDD-HHMMSS/` directory before overwriting
- Preserve file timestamps + modes
- Work regardless of where you cloned the project (agent-memory computes the Claude-Code project-hash from the clone path automatically)

See `agent-context/README.md` and `agent-memory/README.md` for the full mechanism + how to sync updates back to the vendored snapshots after a session writes new memory notes.

Then continue with **Local Development Setup** below for the .NET dev environment.

---

## Local Development Setup

Before running PolarTestApp you need three things: a Polar sandbox token, a webhook secret, and a tunnel so Polar can reach your localhost.

### 1 — Get a Polar sandbox access token

Sign in at [polar.sh](https://polar.sh) → **Settings → Developers → Access Tokens** → create a token. Sandbox tokens start with `polar_oat_`.

### 2 — Store credentials via user-secrets

Run these from inside `testapp/PolarTestApp/`:

```bash
dotnet user-secrets set "PolarSharp:AccessToken" "polar_oat_***"
dotnet user-secrets set "PolarSharp:Webhooks:Secret" "whsec_placeholder"
```

Secrets are stored in `~/.microsoft/usersecrets/` and are never committed to the repo.

### 3 — Install and configure ngrok

Polar's servers cannot reach `localhost` directly — you need a public tunnel.

```bash
brew install ngrok/ngrok/ngrok
```

ngrok requires a free account. Sign up at [dashboard.ngrok.com/signup](https://dashboard.ngrok.com/signup), then configure your authtoken (one-time setup):

```bash
ngrok config add-authtoken YOUR_AUTHTOKEN_HERE
```

Start the tunnel (keep this terminal open):

```bash
ngrok http 5115
```

ngrok prints a public URL like `https://a1b2c3.ngrok-free.app`. This URL changes each time you restart ngrok on the free tier.

### 4 — Register the webhook endpoint in Polar

In the Polar dashboard go to **Settings → Webhooks → Add endpoint**. Set the URL to:

```
https://YOUR-NGROK-URL.ngrok-free.app/hooks/polar
```

Polar generates a `whsec_` secret — copy it and update your user-secret:

```bash
dotnet user-secrets set "PolarSharp:Webhooks:Secret" "whsec_***"
```

Then restart the app. See the full guide — [Local Development Setup](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/local-development.html) — for a complete step-by-step walkthrough including how to send test events.

---

## Documentation

Full documentation — conceptual articles, configuration reference, and complete API reference — is published at:

**[https://mollsandhersh.github.io/Polar.sh_Nuget/](https://mollsandhersh.github.io/Polar.sh_Nuget/)**

The site covers:

| Article | What it answers |
|---|---|
| [Getting Started](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/getting-started.html) | Full annotated `Program.cs` and first API call |
| [Local Development Setup](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/local-development.html) | Sandbox token, user-secrets, ngrok tunnel, webhook testing |
| [Configuration](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/configuration.html) | Every `appsettings.json` field across all 31 packages with valid values and defaults |
| [Webhooks](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/webhooks.html) | HMAC verification, event types, handler registration |
| [Webhook Handlers](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/webhook-handlers.html) | `PolarWebhookHandlerBase<T>`, background queues, idempotency |
| [Webhook Event Reference](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/webhook-event-reference.html) | Every Polar event type, payload shape, and recommended handler pattern |
| [Multi-Tenancy](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/multi-tenancy.html) | Finbuckle strategies, per-tenant bulkhead isolation |
| [Universal Domain Model (BaseEntities)](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/base-entities.html) | The 15 + 6 abstract record bases and how host inheritance eliminates webhook mapping |
| [Identity and Authorization](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/identity-and-authorization.html) | 5-role RBAC + 22-permission ABAC, AppMasterAdmin tier, 5-layer cross-tenant safeguards |
| [Tenant Onboarding](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/onboarding.html) | Programmatic single-call API + resumable wizard with conditional next-steps |
| [Ecommerce Store Management](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/ecommerce-catalog.html) | Local catalog with variants + tiers, 3-tier translation, idempotent publish to Polar |
| [Reporting](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/reporting.html) | Aggregate KPI reports + hierarchical drilldown for Telerik/MudBlazor grids |
| [EF Core Migrations](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/migrations.html) | 12 migration sets, `PolarMigrationRunner<TContext>`, production checklist, CI verification gate |
| [Data Seeding](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/data-seeding.html) | Bogus generators, scale presets, `IsFakeData` toggle, Polar sandbox sync |
| [Toast Notifications](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/toast-notifications.html) | `IPolarToastChannel`, Blazor/SignalR/SSE integration, lazy localization |
| [API Versioning](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/api-versioning.html) | Date-pinned headers, mismatch detection, strictness modes |
| [Security](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/security.html) | Webhook hardening checklist, IP allowlist, token rotation, anomaly detection |
| [Performance Tuning](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/performance-tuning.html) | Connection pool sizing, HTTP/2, hedging, benchmarks |
| [Middleware Pipeline Ordering](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/pipeline-ordering.html) | Exactly where `UsePolarInfrastructure()` goes and why |
| [Test vs Live Mode](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/test-vs-live-mode.html) | Mode banner, token-prefix checks, switching safely |
| [NuGet Deployment](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/nuget-deployment.html) | Publishing to NuGet.org, GitHub Packages, release tagging |
| [Localization](https://mollsandhersh.github.io/Polar.sh_Nuget/articles/localization.html) | `IPolarLocalizer`, built-in `en-US`/`es-MX`, adding new languages |
| [API Reference](https://mollsandhersh.github.io/Polar.sh_Nuget/api/) | Full XML-documented API reference for every public type across all 31 packages |

---

## Packages at a Glance

### `PolarSharp` — Core SDK

The foundation. Everything else builds on top of this.

- **25+ resource areas** — Orders, Subscriptions, Customers, Products, Checkouts, Benefits, Refunds, Discounts, Meters, License Keys, Customer Sessions, Customer Portal, and more — generated from Polar's OpenAPI spec via Kiota
- **`Result<TValue, PolarError>`** returns on every method — typed errors, no exception-for-control-flow
- **`Option<T>`** for nullable fields — explicit, no surprise `NullReferenceException`
- **Automatic retry, circuit breaker, timeout, hedging** (GET/HEAD) via `Microsoft.Extensions.Http.Resilience`
- **Auto idempotency keys** stable across all retry attempts
- **`Polar-Version` date-pinned header** — lock to a known API schema at startup
- **HTTP/2 with connection pooling** and DNS rotation out of the box
- **OpenTelemetry** spans + metrics + health check included
- **PII redaction** in structured logs — customer emails/names never land in your log aggregator
- **Localized error messages** — built-in `en-US` and `es-MX`, extensible via `IPolarLocalizer`
- **Native AOT** — zero ILC warnings, `IsAotCompatible=true`, `IsTrimmable=true`

### `PolarSharp.Webhooks` — Webhook Integration

- **HMAC-SHA256 signature verification** per [Standard Webhooks](https://www.standardwebhooks.com/) spec
- **Multi-secret rotation** — old and new secrets active simultaneously during zero-downtime rotation
- **28 strongly-typed event records** — `OrderCreatedEvent`, `SubscriptionActiveEvent`, `RefundCreatedEvent`, etc.
- **Startup completeness check** — warns (or fails) at launch if known event types have no handler
- **Background queue adapter** (`enqueue: true`) — returns 200 immediately, processes off the request path
- **Webhook reconciliation** — periodic replay of missed events via `polar.Events.ListAsync` for recovery from outages
- **Real-time toast notifications** via `IPolarToastChannel` — feed any Blazor, SignalR, or SSE UI
- **Security hardening** — payload size cap (1 MB), rate limiting, IP allowlist, content-type enforcement, timing-uniform error responses, anomaly detection metrics
- **In-memory idempotency dedup** — optional safety net for at-least-once delivery

### `PolarSharp.MultiTenant` — Multi-Tenant Isolation

- **Per-tenant `PolarClient` instances** — each with its own token, server, and connection pool
- **Per-tenant circuit breakers and rate limiters** via `ResiliencePipelineRegistry<string>` — tenant A's failures are completely invisible to tenant B
- **Race-free initialization** via `LazyConcurrentDictionary` — factory runs exactly once per tenant under any concurrency
- **Finbuckle.MultiTenant integration** — Header, Route, Hostname, or Claim resolution; configured entirely from `appsettings.json`
- **Graceful shutdown** — all per-tenant clients disposed in parallel on `ApplicationStopping`

### `PolarSharp.BaseEntities` — Universal Domain Model

- **15 Polar-native abstract record bases** — `PolarOrderBase`, `PolarCustomerBase`, `PolarProductBase`, `PolarSubscriptionBase`, etc. — property names and types mirror Polar's webhook wire format **byte-for-byte**
- **6 host-additive bases** — `PolarShoppingCartBase`, `PolarCartLineItemBase`, `PolarCategoryBase`, `PolarDepartmentBase`, `PolarInventoryRecordBase`, `PolarSaleBase` — concepts Polar doesn't model that every storefront needs
- **10 wire-format enums** — `PolarOrderStatus`, `PolarSubscriptionStatus`, `PolarRefundReason`, etc. — `[JsonStringEnumConverter]` lock to Polar's exact wire strings
- **Zero external dependencies** — sits at the bottom of every other PolarSharp package's dep tree; safe to inherit in host code without dragging in EF Core, ASP.NET Core, or anything else
- **`abstract record` with `required init`** — immutable, AOT-safe, supports `with`-expressions, allows inheritance
- **Webhook payloads inherit the bases** — `WebhookOrderData : PolarOrderBase` ships in v1.2.0+; host types written against `PolarOrderBase` accept webhook payloads with zero translation

### `PolarSharp.MultiTenant.EntityFrameworkCore` + provider packages — SQL-backed Tenant Store

- **`appsettings.json` path stays unchanged** — these packages are 100% opt-in; v1.1.0 hosts don't pay the EF Core cost unless they install
- **SQL Server + PostgreSQL**: shared DB with EF Core global query filters **AND** database-level Row-Level Security policies (`SECURITY POLICY` / `CREATE POLICY`) for defense-in-depth — a raw-SQL bypass of EF can't leak across tenants
- **SQLite**: one `.db` file per tenant for physical filesystem isolation; shared `__tenants.db` only for bootstrap
- **`IPolarTenantCache`** — Memory + Distributed implementations (wraps `IDistributedCache` for Redis/etc.); invalidated on every tenant write
- **Auto-registered EF Core health checks** with tag `polar-sql` — failures surface in `/health`
- **`PolarMigrationRunner<TContext>` hosted service** — idempotent migration apply on startup; refuses to silently `EnsureCreated` an unversioned schema in Production

### `PolarSharp.MultiTenant.Identity` + provider packages + KeyCloak — Identity, RBAC, ABAC, SSO

- **ASP.NET Core IdentityFramework**, not bespoke auth — `PolarApplicationUser : IdentityUser<Guid>`, `PolarApplicationRole : IdentityRole<Guid>`
- **M:N user↔tenant** via `PolarUserTenantMembership` — one identity, multi-tenant access, distinct role per membership
- **Two role tiers**: SITE-LEVEL `AppMasterAdmin` (SaaS staff with explicit `[AllowCrossTenant]` opt-in) and TENANT-LEVEL (`TenantAdmin`, `TenantUser`, `ReadOnly`, `Auditor`)
- **22-permission `PolarPermission` enum** with `[RequirePolarPermission(...)]` tenant-scoped attribute, `[RequireAppMasterAdmin]` site-level attribute, `[AllowCrossTenant]` cross-tenant opt-in
- **Five-layer cross-tenant safeguards**: EF query filter + database RLS bypass-on-opt-in + authorization attribute + self-elevation prevention + dual audit log (tenant + platform)
- **TenantAdmin invariant** — every tenant has ≥1 active `TenantAdmin` membership; startup `IHostedService` validates; bootstrap of first AppMasterAdmin via config + critical-logged reset token
- **Optional `.KeyCloak` package** — wraps `Microsoft.AspNetCore.Authentication.OpenIdConnect`; realm-role → PolarSharp-role mapping; tenant_id claim propagation; idempotent claims transformation

### `PolarSharp.Onboarding` — Programmatic + Wizard Merchant Onboarding

- **Single-call programmatic API** — `OnboardProgrammaticallyAsync` POSTs `/v1/organizations/` + creates an OAT + registers the webhook endpoint; returns one `OnboardedTenantResult` carrying everything the host needs to start serving the tenant
- **OAuth path** for hosts that prefer user-consent — `BuildAuthorizeUrl` + `CompleteOAuthOnboardingAsync` round-trip
- **Resumable wizard** — `IOnboardingWizard` exposes step-by-step methods with persistent `OnboardingSession` rows (default 7-day TTL); browsers refreshing, multi-day onboarding, and "I'll finish this tomorrow" all just work
- **Conditional next-steps** — `ProductTypes.RequiresMultiLanguage=false` skips the translation step entirely; no irrelevant questions
- **Encrypts in-flight translation API keys** at rest via ASP.NET Core Data Protection — plaintext keys never persist to the session JSON or audit log
- **Auto-provisions a TenantAdmin** on completion via the Identity package's M:N membership table
- **`OnboardingSessionExpirationCleaner` `IHostedService`** — daily prune of stale sessions

### `PolarSharp.EcommerceStoreManagement` + EF Core + Translation providers — Local Catalog, Publish to Polar, AI-Translated Copy

- **Local authoring with idempotent publish** — products, variants (parent + child variants expand to N Polar products on publish), tier groups (Basic/Advanced/Ultimate with cumulative benefit bundles), categories (M:N), discounts, checkout links, business profile
- **Five typed cloning services** — `IProductCloningService`, `ICategoryCloningService`, `IBenefitCloningService`, `IDiscountCloningService`, `ICheckoutLinkCloningService` — all with built-in duplicate prevention (auto-suffixed names, null discount codes, Polar-state reset to Draft)
- **Three-tier translation provider resolution** — per-tenant BYOK (encrypted at rest) → master/SaaS-site config → gracefully disabled
- **Five translation provider packages** — Anthropic, OpenAI, Azure OpenAI, Google Gemini, xAI Grok; each via raw `HttpClient` + JSON, no third-party SDK bulk
- **`catalog_translations` table** — single normalized i18n table covering products/variants/categories/departments/sales/benefits with master-language fallback per field; warm-on-read cache pre-loads translations for predictable cache hits
- **Refund service** wraps Polar `/v1/refunds/` for full + partial refunds with reason codes and audit-logged actor identity
- **`ILicenseKeyValidator`** with short-window caching, grace-period support, and `[RequireValidLicense]` MVC filter
- **Inventory auto-sync** — zero-boundary transitions PATCH `is_archived` on the variant's Polar product; routine stock decrements don't churn the API
- **Admin audit log** via EF Core `SaveChangesInterceptor` — every mutation captured with before/after values, actor identity, timestamp, cross-tenant marker

**About merchant bank-account setup (the "payouts" question):** PolarSharp **does not talk to Stripe**, ever. When a merchant signs up with Polar.sh, they connect a bank account by going to Polar.sh's own web dashboard and following Polar's setup flow there. Polar uses Stripe behind the scenes to move money to that bank, so the merchant may see Stripe-branded screens during the setup — that's normal, and it does not mean your application has any direct relationship with Stripe. PolarSharp's role is limited to (a) generating a clickable link to the right page on Polar's dashboard via `IPolarBusinessProfileService.BuildBankingSetupDeepLink()`, and (b) asking Polar's API whether the setup is complete via `RefreshPayoutStatusAsync()`. The merchant has to do the actual setup themselves in a browser — there is no PolarSharp API shortcut, because Polar's API has no endpoint for it.

### `PolarSharp.Reporting` + EF Core providers — Aggregate KPIs + Hierarchical Drilldown

- **Aggregate / KPI reports** — `TransactionReport` (revenue, refunds, AOV, top products, time buckets), `SubscriptionReport` (MRR, ARR, churn, cohort retention), `OrderReport` (fulfillment latency), `ErrorAuditReport`, `CustomerReport`, `CustomerEntitlementsReport`
- **Hierarchical drilldown** — three lazy methods (`ListCustomersAsync` → `ListOrdersForCustomerAsync` → `GetOrderDrilldownAsync`) designed for hierarchical Telerik / MudBlazor / Blazor grids where the operator opens rows on demand
- **Pre-aggregated snapshot columns** — `OrderCount`, `LifetimeValue`, `LineItemCount`, `RefundedAmount` indexed on the snapshot tables; top-level customer grid loads in <100ms even at 10k customers
- **JSON-first** — every report record `[JsonSerializable]` in source-gen context; client exposes both typed and `*AsJsonAsync()` variants plus a streaming `WriteReportAsJsonAsync(Stream)` for very large payloads
- **Optional snapshot service** — `IHostedService` mirrors `/v1/events/` + `/v1/orders/` + `/v1/subscriptions/` + `/v1/customers/` into local SQL on a schedule; reports query local cache when present, fall back to Polar API otherwise
- **Tenant-scope-aware** — same Finbuckle resolution as webhooks in HTTP requests; bounded `Parallel.ForEachAsync` over all tenants from the tenant store in the background

### `PolarSharp.DataSeeding` — Bulk Fake Data for Sandbox / QA / Demo

- **Six Bogus-backed generators** — Product, Category, Department, LicenseKeysBenefit, Discount, CheckoutLink — each producing base-typed records (`PolarProductBase`-shaped, etc.)
- **Three scale presets** — Demo (10s of records), QA (100s), Stress (10000s) — composable via `SeedFullCatalogAsync`
- **Deterministic seeds** for CI reproducibility — same `randomSeed` → same output across runs
- **`IsFakeData=true`** on every record via the `IFakeDataAware` interface; a dual EF Core global query filter hides fake data from every query/report/publish when the per-tenant `AllowFakeData` toggle is OFF
- **`FakeDataToggleChanged`** domain event + `FakeDataSyncService` `IHostedService` — flipping the toggle OFF↔ON syncs fake data to/from Polar's sandbox automatically
- **`Metadata["polar_sharp_is_fake_data"]="true"`** on every published fake record so the snapshot ingester preserves the fake-data marker end-to-end
- **Dev-time package** — see the AOT-publish note in [Features That Actually Matter](#features-that-actually-matter) above; gate behind `#if DEBUG` or suppress trim warnings via `<TrimmerRootAssembly>`

---

## Compatibility

| Target | Supported |
|---|---|
| .NET 10 | ✅ |
| Native AOT | ✅ |
| ASP.NET Core Minimal API | ✅ |
| ASP.NET Core MVC | ✅ |
| Blazor Server | ✅ |
| Azure Functions (isolated worker) | ✅ |
| Worker Service / `IHostedService` | ✅ |

---

## Disclaimer and Legal Notices

### No Affiliation with Polar.sh

PolarSharp is an independent, community-developed open-source library. The author has **no affiliation, partnership, sponsorship, or relationship of any kind** with Polar.sh, Polar Software Inc., or the operators of the polar.sh website. PolarSharp is not endorsed by, certified by, or in any way associated with Polar.sh. All Polar.sh trademarks, service marks, and brand names are the property of their respective owners.

### No Warranties — Use at Your Own Risk

THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, ACCURACY, RELIABILITY, OR NON-INFRINGEMENT. THE AUTHOR MAKES NO REPRESENTATIONS OR WARRANTIES THAT THIS LIBRARY:

- meets any particular technical, regulatory, compliance, or business requirements;
- is free of defects, bugs, or security vulnerabilities, known or unknown;
- will function correctly with any particular version of the Polar.sh API;
- is suitable for use in production, financial, medical, legal, or any other regulated environment; or
- has been independently audited, vetted, reviewed, or tested by any person or organization other than the author.

**USE OF THIS LIBRARY IS ENTIRELY AT YOUR OWN RISK.** You are solely responsible for evaluating the suitability of this software for your use case and for all consequences arising from its use.

### Independent Testing Disclosure

PolarSharp has been designed and tested by its author to the best of their ability. It has **not** been independently audited, penetration-tested, security-reviewed, or certified by any third-party security firm, compliance body, or independent testing organization. No claim of security certification or compliance certification (PCI DSS, SOC 2, ISO 27001, GDPR, or otherwise) is made for this library.

### Security Features — No Guarantee

PolarSharp was designed with enterprise-class security in mind and incorporates significant defensive measures including, but not limited to: HMAC-SHA256 webhook signature verification, timing-uniform error responses, payload size enforcement, rate limiting, IP allowlisting, TLS 1.2+ enforcement with certificate revocation checking, SSRF mitigation, and anomaly detection metrics. These features are intended to provide meaningful protection against common attack vectors including denial-of-service attacks, replay attacks, and webhook forgery.

**However, no software can guarantee protection against all known or unknown attack vectors.** The threat landscape evolves continuously, and no representation is made that the security controls in this library will be effective against all present or future attack techniques. It is your responsibility to perform your own security assessment, keep dependencies up to date, monitor for vulnerabilities, and apply additional controls appropriate to your environment and risk tolerance.

### Limitation of Liability

IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY CLAIM, DAMAGES, OR OTHER LIABILITY — WHETHER IN AN ACTION OF CONTRACT, TORT, OR OTHERWISE — ARISING FROM, OUT OF, OR IN CONNECTION WITH THIS SOFTWARE OR THE USE OR OTHER DEALINGS IN THIS SOFTWARE, INCLUDING ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, CONSEQUENTIAL, OR PUNITIVE DAMAGES, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGES. This includes, without limitation, any financial losses, fraudulent transactions, data breaches, regulatory penalties, or business interruptions arising from the use or inability to use this software.

### Third-Party Services

This library communicates with the Polar.sh API. Your use of Polar.sh is subject to Polar.sh's own terms of service, privacy policy, and acceptable use policy, which are independent of and unrelated to this library and its author.

---

## License

MIT — see [LICENSE](LICENSE).

---

*Built with Kiota, Microsoft.Extensions.Http.Resilience, and Finbuckle.MultiTenant.*
*Fully generated API reference and conceptual docs at [mollsandhersh.github.io/Polar.sh_Nuget](https://mollsandhersh.github.io/Polar.sh_Nuget/).*
