---
name: Every Polar HTTP wrapper ships with a live-through-our-class integration test
description: For this repo, each new IPolarXxxApi wrapper class (or equivalent Polar HTTP boundary) MUST ship with a live integration test that goes user-code → our wrapper → live Polar sandbox. Pure mocks aren't enough.
type: feedback
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
**Rule:** every wrapper class that calls the Polar.sh HTTP API ships with a paired live integration test that:

1. Reads `POLAR_SANDBOX_TOKEN` from env (loaded via direnv from `.env` locally; from GitHub Actions secret in CI)
2. Constructs a real `HttpClient` with the Bearer header and `BaseAddress = https://sandbox-api.polar.sh`
3. Builds a `PolarClient` with that HttpClient
4. Constructs OUR wrapper class (e.g. `PolarClientRefundsApi`, `PolarClientLicenseKeysApi`, `PolarClientOrganizationsApi`)
5. Calls the wrapper's public method
6. Asserts on the typed `Result<TResponse, TError>` that came back

**Forbidden in these tests:** direct `HttpClient.SendAsync`, direct curl-equivalent, raw Kiota calls that bypass our wrapper. The wrapper IS the system under test. If the test doesn't exercise the wrapper, it's not the right test.

**Skip pattern when token absent:**
```csharp
if (string.IsNullOrEmpty(Token)) return;     // sandbox token not provided — silently skip
```
The test no-ops on machines without sandbox access (e.g. fork PR builds) so local-unit-test runs stay green.

**[Trait("Category", "Integration")]** marks every such test. CI's `Integration Tests (sandbox)` job picks them up via `--filter Category=Integration`; the default `--filter "Category!=Integration"` from the build job ignores them.

**Why:** project owner explicitly requested 2026-05-14: "yes mocking is important to ensure that our classes/records and their methods operate as expected, but we also should test that the libraries are working correctly against live calls from our specific methods and not just having direct calls made through httpclient in the unit tests themselves." Mocks alone don't catch: Kiota model drift when the OpenAPI spec updates, response-shape changes Polar pushes mid-version, auth scope mismatches, payload byte-format edge cases (UUID v4 requirement, trailing-slash redirects we discovered in V20-002/003).

**How to apply:**

- For every new wrapper class I write: pair it with a `tests/<package>.Tests/PolarClient<Resource>ApiIntegrationTests.cs` file following the V20-002 / V20-003 / V20-004 template.
- Live tests should cover at minimum:
  - One happy path (real-org GET, listing with a valid filter, etc.) where I can verify against known state — `PolarClientOrganizationsApiIntegrationTests.GetAsync_against_real_sandbox_org_returns_id_and_account_fields` is the gold-standard example
  - One typed-error path (404 against non-existent, 400/422 against malformed input) to prove the wrapper never lets an exception leak
  - One mutation path WHERE SAFE — use a non-existent target id when possible so the live test doesn't churn real sandbox state on every CI run
- For older v1.1.0 PolarClient surfaces that DON'T have a PolarSharp-owned wrapper: skipping is acceptable for now; if and when a wrapper gets added, it gets a live test.

**What this rule does NOT require:**

- Receiving actual webhooks from Polar's sandbox (requires public URL infrastructure; out of scope, documented as a known gap)
- Live tests on AI translation providers (those test against their own services, not Polar)
- Live tests on every Kiota-generated method (only on the ones PolarSharp wraps)

**Verified compliance as of 2026-05-14:** V20-002 (refunds, 2 tests), V20-003 (license keys, 2 tests), V20-004 (organizations, 3 tests). All future V20-NNN HTTP-completion tasks ship with the same pattern.
