---
name: Verify .NET 10 framework capabilities before recommending build-vs-use
description: Don't claim "X isn't supported in .NET 10, needs community library" from training cutoff knowledge; verify via WebSearch first
type: feedback
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
When recommending whether PolarSharp needs to build / integrate a community library / wait for a future .NET version, DO NOT rely on training-cutoff knowledge about what's in or not in .NET 10 / ASP.NET Core.

**Why:** During the 2026-05-19 WC catalog walkthrough I told the user that ASP.NET Core Identity in .NET 10 didn't have native passkey support and recommended a Fido2.NetFramework integration as a 1-2 week project deferred to v1.4.x. The user challenged me; I verified via WebSearch and found Microsoft Learn documentation showing passkey support IS native in .NET 10 (`view=aspnetcore-10.0`). The work scope shrank from 1-2 weeks to 1-2 days, and the catalog impact moved from v1.4.x to v1.4.0.

**How to apply:**
- Before claiming a .NET framework feature is missing / not yet shipped, run `WebSearch` with the specific feature + .NET version + current year (2026).
- Authoritative sources to prioritize: `learn.microsoft.com/en-us/aspnet/core/...?view=aspnetcore-10.0` URLs (note the `view=` parameter — Microsoft Learn pins doc versions explicitly).
- Especially relevant when the user pushes back with "I thought X was supported" — treat that as a signal to verify rather than dig in.
- The training-cutoff date doesn't reliably tell me what shipped on a given date; release notes change, preview features land in stable versions, etc.
