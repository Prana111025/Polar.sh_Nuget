# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Purpose

This repository will contain a .NET NuGet package integrating with [Polar.sh](https://polar.sh) — an open source monetization platform. The package is expected to target .NET and follow standard NuGet packaging conventions.

## Required Reading Order

Before doing any work, read these files in order:

**Project-rooted (all in this repo at `/Users/mollsandhersh/Repos/Polar.sh_Nuget/`):**

0. **`LARGE-PROJECT-BEST-PRACTICES.md`** ⭐ READ FIRST — the methodology white paper. Codifies patterns, anti-patterns (the Trap Catalog), tool decision trees, agentic-AI-specific protocols, and reusable templates developed during the 2026-05-19 architecture session. Contains the Quick-Reference Card at the top (Top 10 patterns + Top 10 anti-patterns) for fast cold-start scanning. Mandatory reading for both human contributors AND agentic AI sessions.
1. `PLAN.md` — active technical plan
2. `TASKS.md` — current task list
3. `PROGRESS.md` — completed work log
4. `DECISIONS.md` — locked architecture decisions
5. The 5 Architectural Case Studies in `Case Studies/` (see list below)

**Home-rooted (in `/Users/mollsandhersh/`; universal across all Claude projects):**

6. `AGENTS.md` — workflow rules, agentic-master policy, RAG policy (applies across all Claude projects)
7. `ZoranHorvat.md` — required for all .NET/C#/NuGet work (applies across all .NET projects)

**Location note (clarified 2026-05-19):** Items 1–4 used to live in the home directory `/Users/mollsandhersh/` alongside AGENTS.md + ZoranHorvat.md. That was a single-project-era design that didn't scale to multiple Claude projects on the same machine — multiple projects following the same home-level CLAUDE.md instruction would have collided on shared planning files. The 4 files were moved into the project folder on 2026-05-19 so each Claude project on this machine has its own PLAN/TASKS/PROGRESS/DECISIONS without cross-project pollution. Items 6 + 7 stay home-rooted because they apply universally across projects.

**The 5 Architectural Case Studies in `Case Studies/`:**

   - `01-Lift-And-Shift-Architecture.md` — visible namespace separator + CI dependency guard pattern for monorepo features designed for eventual extraction
   - `02-Event-Sourced-Wallet-With-Economic-Modeling.md` — wallet design + economic transparency + Polar.sh as the SaaS-tenant on-ramp
   - `03-Embed-Anywhere-Web-Components.md` — Stencil + Shadow DOM + 11-layer server-as-source-of-truth fraud prevention for embeddable widgets
   - `04-Audience-Scoped-Schema-Slicing.md` — LLM-driven query generation with audience-scoped schema slices as structural defense-in-depth
   - `05-Multi-Tenancy-As-Optional.md` — mode-agnostic library design for serving both single-tenant and multi-tenant deployments from one codebase

## Build and Test

> Update this section once the project structure is established.

Expected commands once `.csproj` / `.sln` files exist:

```sh
dotnet build
dotnet test
dotnet pack           # produces the NuGet .nupkg
dotnet nuget push     # publish to NuGet feed
```

## Git and GitHub Policy

Do **not** run raw mutating Git/GitHub commands. Use `agentic-master` wrappers:

```sh
agentic-master new-task <TASK-ID>
agentic-master commit --ai
agentic-master push
agentic-master finish-task
agentic-master verify
```

See `~/AGENTS.md` for the full command reference.

## Architecture Notes

> Populate this section once source files exist.

- This is a .NET library project — apply ZoranHorvat.md coding standards to all `.cs` and `.csproj` files.
- NuGet package metadata (authors, description, version, license) belongs in the `.csproj` file via `<PackageId>`, `<Version>`, `<Description>`, etc., not in a separate `nuspec`.
- Public API surface should be minimal and intentional — treat every public type as a committed contract.

## Documentation Standards (PROJECT-WIDE, STANDING REQUIREMENT)

Every feature shipped in this repo must land with **complete documentation across all three surfaces** — there is no "we'll write the docs later" posture. This is a standing project requirement, not a per-feature ask.

### The three surfaces, in priority order

1. **Inline XML doc comments** (`///`) — every public type and every public member. CS1591 is build-error in `Directory.Build.props`. Cover `<summary>`, `<remarks>` for the *why*, `<param>` for each parameter, `<returns>`, `<exception>` for directly-thrown types. For public methods/extension methods also include `<example>` with compilable code.
2. **Per-package `README.md`** — every NuGet package ships its own README via `<PackageReadmeFile>README.md</PackageReadmeFile>`. Install command + ~5-line quickstart + link to the docs site.
3. **GitHub.io DocFX site** — conceptual articles for every capability area. Every new package adds at least one article to `docs/articles/*.md` and the corresponding entry to `docs/articles/toc.yml`. The DocFX `docfx build` step gates CI — broken cross-references fail the build.

### Implementation Narratives section (NEW — DocFX site)

The DocFX site has a dedicated **"Implementation Narratives"** top-level section providing approachable, audience-friendly guides covering:
- Every reusable Razor component shipped in `PolarSharp.UI.Components` (each component gets its own narrative)
- Major end-to-end workflows that touch multiple capability areas (e.g., "Onboarding a new merchant from sign-up through first sale", "Setting up payouts when you've never used Stripe before", "Translating your catalog into a new market")

The Narratives section is **distinct from the API Reference and the Articles** by tone and audience: API Reference is for developers, Articles are for system-design readers, Narratives are for **non-technical stakeholders** — product owners, merchant operators, sales/support staff — who need to understand what a feature does and when to use it without becoming developers.

### Writing-style rules for Implementation Narratives

- **Friendly, conversational tone.** Write like you're explaining the system to a smart colleague over coffee, not lecturing a class.
- **Audience: non-technical.** Assume the reader is sharp but doesn't know what a SignalR hub, a Razor component, an EF Core query filter, or an OAuth flow is.
- **Lean on analogies.** Hard concepts get explained by comparison to familiar things:
  - *"Think of the multi-tenant filter like the front desk at a hotel — every request shows their room key (tenant id), and the desk only hands them mail addressed to their room."*
  - *"The toast channel works the way a sports score app pushes updates to your phone — the server pushes when something happens, you don't have to keep refreshing."*
  - *"A Razor component is like a Lego brick — built once with a specific shape, snapped into many different pages without changing the brick itself."*
- **Concrete scenarios beat abstract descriptions.** Every narrative opens with "Suppose a merchant wants to …" and walks through the user-visible flow first, with the mechanics second.
- **Call out options and notes explicitly.** Every narrative ends with a "Things to know" subsection listing optional parameters, edge cases, and gotchas in plain language.
- **No jargon without an explanation.** First use of any technical term gets a parenthetical or footnote. Subsequent uses can assume the term is known within the narrative.
- **Short paragraphs, generous whitespace.** This is documentation people will read on their phones during onboarding meetings, not engineering reference material.

### Critical framing rules — DO NOT VIOLATE in any Narrative or doc

These are framing rules that have been explicitly established with the project owner and committed to multiple doc surfaces (see commit `1347e01`). They MUST be reflected in every piece of documentation, including Narratives:

- **PolarSharp does NOT talk to Stripe. Ever. Anywhere.** Do not write any documentation that suggests, implies, or even casually mentions "your application uses Stripe" or "PolarSharp integrates with Stripe". When discussing payouts:
  - The merchant signs up with **Polar.sh**, not Stripe.
  - The merchant connects their bank account in **Polar.sh's own dashboard**, not in your application and not through any PolarSharp API.
  - Polar.sh happens to use Stripe internally as the rails for moving money to merchant bank accounts. The merchant may see Stripe-branded screens **inside Polar.sh's dashboard** during the bank-account setup. That is normal and is a Polar.sh design detail, not a PolarSharp concern.
  - Your application's only role in the payout story is: (1) showing a link to the right Polar.sh dashboard page via `IPolarBusinessProfileService.BuildBankingSetupDeepLink()`, and (2) asking Polar's API whether the setup is complete via `RefreshPayoutStatusAsync`. That's it.
  - When writing Narratives about payouts, frame the topic as "connecting a bank account" or "setting up payouts" — never as "setting up Stripe" or "Stripe Connect onboarding".

If a draft Narrative or doc accidentally references Stripe in a way that implies PolarSharp talks to Stripe, REWRITE IT before shipping. Use the inline XML docs on `IPolarBusinessProfileService.BuildBankingSetupDeepLink` + the "Banking and payouts" section in `docs/articles/ecommerce-catalog.md` as the reference for correct framing.

### When this requirement applies

- Every new public type → inline XML docs at minimum
- Every new package → README + DocFX article
- Every new RCL component → Implementation Narrative
- Every major workflow change → Implementation Narrative covering the user-visible impact
- Every release → CHANGELOG entry summarising what readers need to know

### MAUI / multi-platform dev setup documentation (binding for v1.5.0+)

When the `PolarSharp.UI.Components.Maui` RCL + `PolarMauiDemo` flagship land in v1.5.0, the DocFX `local-development.md` article MUST gain a dedicated **"Setting up .NET MAUI for PolarSharp development"** section. The section must cover, concretely:

- **Required SDK workloads** with exact CLI: `dotnet workload install maui` (bundle) OR individually `maui-android`, `maui-ios`, `maui-maccatalyst`, `maui-windows`. Plus `dotnet workload list` to verify and `dotnet workload update` to maintain currency.
- **Per-OS tooling** for each supported dev environment:
  - macOS — Xcode + Command Line Tools (required for iOS / Mac Catalyst), Apple Developer ID + provisioning profiles, Android Studio + Android SDK + AVD emulators, JDK 17+
  - Windows — Visual Studio 2026 with the ".NET Multi-platform App UI development" workload, Android SDK + emulator, Hyper-V or WSL2 for emulator acceleration, optional Mac pairing for iOS via Hot Restart
  - Linux — Android only (no iOS / Mac Catalyst support is possible), JetBrains Rider or VS Code with the .NET MAUI extension
- **Simulators / emulators** — install steps, CLI launch commands (`xcrun simctl boot`, `emulator @AvdName`), pairing to the IDE
- **Signing certs + provisioning profiles** — explicit guidance on what's needed for running on a real device vs. just simulator; how to manage Apple provisioning expiry; Android signing key generation
- **Telerik UI for Blazor on MAUI** — confirmation that the Telerik license file works identically for MAUI Blazor Hybrid as for web (per-developer, not per-platform); how to share licensing across Web + Mobile dev work
- **First-run validation** — a command sequence the reader can paste to prove their setup works: `dotnet new polar-mobile-companion -n FirstMauiTest && cd FirstMauiTest && dotnet build -f net10.0-android`
- **Common pitfalls** — Android SDK version mismatches, iOS provisioning expiry, MacCatalyst capability entitlements, Hot Restart limitations, .NET workload mismatch errors

Style: the conceptual framing parts (why workloads exist, when you need them, what's optional vs. required) follow the Implementation Narratives style — friendly, audience-friendly, analogies for hard concepts ("think of each workload as a separate language pack you install based on which platforms you want to target"). The reference parts (exact commands, exact CLI flags, exact prerequisite versions) stay precise and copy-pasteable.

This standard is encoded here so it carries forward across sessions, contributors, and feature waves without needing to be re-asked.

## Architectural notes that apply to all work

### DI extension methods auto-wire MediatR

When a PolarSharp package needs MediatR (currently: PolarSharp.MultiTenant + PolarSharp.MultiTenant.Notifications + PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite + the wallet's notification dispatcher), the package's `AddPolarXxx(...)` extension method registers MediatR internally for that package's own assembly. Host code does NOT need to call `services.AddMediatR(...)` separately. MediatR's `AddMediatR` is idempotent across multiple calls — multiple packages each calling `AddMediatR` with their own assembly compose into one MediatR instance with handlers from all assemblies discoverable. For the full decision tree on which PolarSharp extensions to call in which scenario, see `docs/articles/narratives/choosing-your-polarsharp-di-wiring.md`.

### Required reading at session start

Per the "Required Reading Order" section above, every session that touches PolarSharp code should load the 5 Case Studies. They contain the architectural patterns this codebase is built around (lift-and-shift, event sourcing, embeddable web components, audience-scoped schema slicing, optional multi-tenancy). Decisions in code reviews should reference the relevant case study where applicable.
