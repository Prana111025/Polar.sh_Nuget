# Implementation Narratives

Welcome to the friendliest part of the PolarSharp documentation. The other articles on this site are written for software developers — they assume you know what a database query filter is, what an OAuth flow does, what a Razor component compiles into. **The Narratives are different.** They're written for everyone else: product owners, merchant operators, sales engineers, support staff, executives, and anyone who needs to understand what PolarSharp does and when to reach for which piece — without needing a computer-science degree first.

## What you'll find here

Two kinds of narratives:

**Component Narratives** — one per reusable UI component in the `PolarSharp.UI.Components` package. Each narrative explains, in plain language: *What is this thing? When would I use it? What does it look like to the merchant? What options do I have? What should I watch out for?*

**Workflow Narratives** — one per major end-to-end story that touches multiple parts of the system. Examples: "Onboarding a brand-new merchant from sign-up through first sale", "Setting up payouts when you've never used Stripe before", "Translating your product catalog into a new market". These walk through the user's journey first, then explain what's happening behind the scenes in just enough detail to be useful.

## How we write these

A few rules we keep ourselves to, so these stay readable:

- **Friendly tone, not formal.** Like explaining something to a smart colleague over coffee.
- **Analogies for hard concepts.** When something is genuinely complicated, we use comparisons to things you already understand — hotels, mail rooms, sports scores, Lego bricks — so the idea sticks before the jargon does.
- **Concrete scenarios first.** Every narrative opens with *"Suppose a merchant wants to…"* and walks through the visible flow. The technical mechanics come second, only as much as you need.
- **Plain language for technical terms.** The first time we use a term like "webhook" or "SignalR" or "Blazor", we explain it in a parenthetical. After that we trust you remember.
- **Short paragraphs, generous whitespace.** These docs should be readable on a phone during an onboarding meeting, not just on a developer's wide monitor.
- **A "Things to know" section at the end.** Every narrative ends with the gotchas, optional settings, and edge cases — listed in plain language so you can scan them quickly.

## Setting up PolarSharp in your app

Narratives that walk through how the pieces fit together when you wire PolarSharp into your own application.

- [**Choosing your PolarSharp DI wiring**](choosing-your-polarsharp-di-wiring.md) — the three most common deployment shapes (minimum, middle, and full) laid out side by side, with a plain-language explanation of why multiple PolarSharp packages can all "register MediatR" without conflicting and what actually happens when you install a package but forget to wire it up. The narrative everyone reading their startup file for the first time wishes existed.
- [**Cart and checkout, in plain language**](storefronts-cart-and-checkout-for-customers.md) — what actually happens when a customer adds something to their cart, applies a discount, types in their address, and hits "Pay". Covers the server-as-source-of-truth fraud-prevention discipline, the in-page progress streaming as each checkout stage runs, and the small set of "things to know" gotchas every storefront operator should be aware of.

## Coming soon

This section is brand-new. We're populating it as we build out the `PolarSharp.UI.Components` package and the flagship `PolarSaasDemo` reference application. Expect the first narratives to land alongside v1.4.0, covering:

- **`PolarKpiTile`** — the little dashboard cards that show numbers like "Revenue this month" or "Active subscribers"
- **`PolarHierarchicalGrid`** — the table-with-expanding-rows that lets you drill from a customer down to one of their orders down to the line items
- **`PolarLiveToastSubscriber`** — the piece that makes the dashboard update *automatically* when something happens at Polar.sh, without you hitting refresh
- **`PolarTenantContextBar`** — the indicator at the top that tells you which merchant you're currently looking at (relevant when one person manages multiple merchants)
- **`PolarPermissionGate`** — the rule-enforcer that hides parts of the screen from people who shouldn't be able to use them
- **Onboarding a new merchant end-to-end** — what happens from "I want to sign up" through "I just received my first payment"
- **Connecting a bank account so the merchant can receive payouts** — the merchant does this themselves in Polar.sh's own dashboard (PolarSharp does not handle bank-account setup — it shows the merchant a link to the right page on Polar's website and later checks whether Polar reports the setup as complete). Why PolarSharp is intentionally hands-off here, and what the merchant actually sees on their screen.
- **Translating your catalog** — how the multi-language story works, what the merchant decides vs. what runs automatically

If you arrive at a "Coming soon" link, that narrative is on the schedule but hasn't been written yet. Check back after the next release, or open an issue in the GitHub repo if you'd like us to prioritise a particular one.
