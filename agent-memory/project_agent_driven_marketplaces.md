---
name: Agent-driven marketplace construction model
description: PolarSharp tenant websites will be constructed by agentic AI agents that decision-tree-select WCs from the catalog; documentation must be machine-parseable
type: project
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
The user's deployment model for PolarSharp marketplaces is **agent-driven website construction**:

1. The user spins up many ("army of") marketplaces, each in a different niche (vintage keyboards, fountain pens, etc.)
2. An agentic AI agent constructs each marketplace's website by:
   - Working through a decision tree about what features the marketplace needs (sells physical goods? subscriptions? B2B? high-volume?)
   - Examining the full PolarSharp WC catalog documentation
   - Selecting which WCs to include
   - Determining layout + wiring (composition, data sources, tenant config)

**Why:** Sets the scope tolerance higher than human-onboarding-friendly products would justify. The catalog can be deliberately large (~50+ WCs) because agents filter mechanically. "Different from and better than Shopify" is the explicit goal.

**How to apply this for future work:**

- **Every WC needs machine-parseable doc frontmatter** in addition to human-readable narrative. Required fields: purpose, data-source (existing PolarSharp service or new-feature-needed), dependencies (other PolarSharp services / config), composes-with (commonly-paired WCs), conflicts-with (WCs that shouldn't coexist), required-tenant-config, audience (anonymous / authenticated), deployment-context (embed-anywhere / tenant-storefront-only), decision-tree-tags (e.g. physical-goods, subscription-business, B2B, high-volume).
- **WebComponent Styling Guide narrative becomes mission-critical** — agents won't author CSS but will generate hosts that wire WCs; the styling guide is one of their reference docs.
- **Per-WC ship target matters for agents** — v1.4.0 vs v1.4.x vs v1.5+ tags need to be in the doc frontmatter so the agent knows what's actually available when generating the marketplace.
- **Catalog scope per-niche matters more than per-tenant.** Niche profiles (e.g. "vintage-keyboards") could be a future feature where the agent gets a curated WC + theme starter set per niche, then composes from there.
- **Documentation quality directly impacts deployment quality.** A confusingly-documented WC will get omitted or misused by agents. Worth more investment than a normal CLI tool would warrant.
