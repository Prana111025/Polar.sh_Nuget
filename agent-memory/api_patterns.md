---
name: PolarSharp API Patterns
description: Kiota EmptyPathSegment pattern, Finbuckle correct method names, PolarClient resource access — avoid repeating research
type: feedback
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
## Kiota `EmptyPathSegment` pattern for list endpoints

Kiota generates `EmptyPathSegmentRequestBuilder` for collection list endpoints:

```csharp
// List all orders (EmptyPathSegment = collection GET):
var list = await polar.Orders.EmptyPathSegment.GetAsync(cancellationToken: ct);

// Get single order by ID (indexer = item GET):
var order = await polar.Orders["ord_123"].GetAsync(cancellationToken: ct);
```

This applies to ALL resources: Orders, Subscriptions, Customers, Products, Checkouts, Benefits, BenefitGrants, Discounts, LicenseKeys, Meters, CustomerSessions, etc.

**Why:** Kiota's code generator uses `EmptyPathSegment` for the trailing-slash version of the collection URL to avoid ambiguity with the parameterized item URL.

## Finbuckle method name (confirmed via DLL strings)

`app.UseMultiTenant()` — NOT `app.UseMultiTenancy()`. Namespace needed:
```csharp
using Finbuckle.MultiTenant.AspNetCore.Extensions;
```

**Why:** Method was verified by analyzing Finbuckle 9.x DLL strings. `UseMultiTenancy` does not exist.

## PolarClient does NOT implement IAsyncDisposable/IDisposable

Avoid pattern-matching `PolarClient` against `IAsyncDisposable` or `IDisposable` — it's a sealed class that implements neither. `HttpClient` instances from `IHttpClientFactory` are managed by the factory's pooling infrastructure, not by the consumer.

**Why:** CS8121 compile error when doing `case IAsyncDisposable d:` against a known-sealed type that doesn't implement it.

## ITenantInfo nullability (Finbuckle 9.x)

`ITenantInfo.Id` and `ITenantInfo.Identifier` are `string` (non-nullable). Implementing class must use non-nullable properties with empty string defaults:

```csharp
public string Id { get; set; } = string.Empty;
public string Identifier { get; set; } = string.Empty;
```

**Why:** CS8766 mismatch if declared as `string?` in implementation when interface declares `string`.

## CustomerSessions API shape

```csharp
// Create a session:
var body = new EmptyPathSegmentRequestBuilder.PostRequestBody
{
    CustomerSessionCustomerIDCreate = new() { CustomerId = "cus_xxx" },
};
var session = await polar.CustomerSessions.EmptyPathSegment.PostAsync(body, cancellationToken: ct);
// session.Token is the Customer Access Token
// session.CustomerPortalUrl is the ready-to-use portal URL

// Create portal client from token:
var portalClient = polar.CreateCustomerPortalClient(session.Token);
var orders = await portalClient.Orders.EmptyPathSegment.GetAsync(cancellationToken: ct);
```

## PolarApiMetadata is internal — use PolarClient.GeneratedAgainstVersion

`PolarApiMetadata` (in `PolarSharp.Versioning`) is `internal`. To read the SDK's generated-against version from outside the assembly, use the public static property:

```csharp
string version = PolarClient.GeneratedAgainstVersion;
```
