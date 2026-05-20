# AGENT-COORDINATION.md

This file governs **parallel agent work** on PolarSharp. It is mandatory reading for any agent (Claude Code sub-agent, Cursor agent, Aider, etc.) that operates on this repository while OTHER agents may also be working. Solo sessions can skim it for the branch-naming convention and otherwise ignore it.

The user explicitly committed (2026-05-19) to "no shortcuts" on parallel agentic development: every agent ships full inline XML docs, per-package READMEs, DocFX articles, Implementation Narratives, ≥90% unit-test coverage, and the lift-shift CI guard where applicable. Coordination overhead exists to make those commitments holdable, not optional.

---

## 1. Branch-per-agent (NON-NEGOTIABLE)

No sub-agent commits directly to `main`. Every agent works on a feature branch and asks the user to merge.

### Branch naming convention

```
agent/<feature-area>/<phase-or-task-id>-<short-slug>
```

| Feature area | Examples |
|---|---|
| `wallet` | `agent/wallet/phase-20-event-store`, `agent/wallet/phase-21-ef-providers` |
| `storefronts` | `agent/storefronts/phase-25-cart-checkout`, `agent/storefronts/phase-28-web-components` |
| `docs` | `agent/docs/v1.3-docfx-sweep`, `agent/docs/wallet-narrative` |
| `db-providers` | `agent/db-providers/phase-13-mariadb` |
| `coordination` | `agent/coordination/<task>` — reserved for shared-file edits orchestrated by the main session |

Rules:
- Branch off `main` (never off another agent's branch — keeps merge order flexible).
- One agent → one branch at a time. Finish + merge before starting the next.
- Long-running branches (>2 days) rebase on `main` daily so merge stays trivial.

### Merge gate

Before requesting merge, the agent runs:

```sh
./scripts/pre-merge-gate.sh
```

The script returns non-zero on any failure. **Merge requests with a failing gate are rejected at the door.** See section 5 for what the gate checks.

---

## 2. File-ownership matrix — week of 2026-05-19

This week's three agents work on strictly non-overlapping `src/` subtrees plus disjoint sections of shared files. Update this table when scaling up.

### `src/` ownership (exclusive)

| Agent | Owns `src/` subtree | Owns `tests/` subtree |
|---|---|---|
| **A — wallet** | `src/PolarSharp.PrepaidWallets.*` AND `src/PolarSharp.PrepaidWallets.Polar.*` AND `src/PolarSharp.PrepaidWallets.AspNetCore.*` | `tests/PolarSharp.PrepaidWallets.*` |
| **B — storefronts** | `src/PolarSharp.EcommerceStorefronts.*` AND `src/PolarSharp.EcommerceStorefronts.Polar.*` | `tests/PolarSharp.EcommerceStorefronts.*` |
| **C — docs** | (none in `src/`; doc-only commits) | (none in `tests/`; doc-only commits) |

If Agent A needs to touch a file Agent B owns (or vice versa), the change is escalated to the user via the merge-request comment. Cross-tree edits without explicit coordination are rejected.

### Documentation ownership (exclusive)

| Agent | Owns `docs/articles/` paths |
|---|---|
| **A — wallet** | `docs/articles/prepaid-wallets-*.md` AND `docs/narratives/wallets-*.md`, `docs/narratives/setting-up-customer-prepaid-wallets-*.md`, `docs/narratives/keeping-money-in-the-family-*.md`, `docs/narratives/accepting-purchase-orders-*.md`, `docs/narratives/letting-customers-know-things-*.md` |
| **B — storefronts** | `docs/articles/storefronts-*.md` AND `docs/narratives/storefronts-*.md`, `docs/narratives/*-marketplace*.md` |
| **C — docs** | EVERYTHING ELSE in `docs/` (refactoring, cross-link fixes, narrative sweeps for already-shipped features, the appsettings reference, the DocFX TOC) |

### PLAN.md sectioned ownership

`PLAN.md` is a single 97KB file that every phase touches. Until we split it into per-feature includes (deferred), each agent edits only their assigned section markers:

| Agent | PLAN.md sections it may edit |
|---|---|
| **A — wallet** | "Prepaid wallets — design detail" subsection AND any "v1.3.0 amendments" entries marked `[wallet]` |
| **B — storefronts** | "v1.4.0 expansion — PolarSharp.EcommerceStorefronts" section |
| **C — docs** | "Open questions" + "Key decisions recorded" tables only (append rows; never delete) |

If an agent's work materially changes the architecture in another agent's section (rare), it writes a one-paragraph note in the merge-request body describing the change; the user reconciles at merge time.

---

## 3. Shared-file rules — LOCKED by default

These files are touched by almost every feature. Sub-agents must NOT edit them directly:

| File | Why locked | Path forward |
|---|---|---|
| `Directory.Packages.props` | Every new NuGet package addition lands here; concurrent edits = nightmare merges | Agent notes the package + version in its merge-request body; user (or the coordination session) adds it before the agent merges |
| `PolarSharp.slnx` | Every new csproj registers here | Same |
| `CLAUDE.md` (project root) | Standing project requirements; rarely changes | Agents propose changes via merge-request body |
| `README.md` (project root) | User-facing repo intro | Same |
| `Directory.Build.props` | Project-wide build settings | Same |
| `AGENT-COORDINATION.md` (this file) | Coordination doctrine | User-edited only |
| `PROGRESS.md` | Roll-up log; ordering matters | User-edited at merge time |

**Exception**: if an agent's work absolutely requires adding a package or a project, the agent does the edit, but the merge-request body explicitly flags "TOUCHED LOCKED FILE: Directory.Packages.props (added PackageX 1.2.3 for Phase Y)". The user reviews the diff and merges in an order that resolves any conflicts.

---

## 4. Memory-write rule

Claude Code persists per-project memory under `~/.claude/projects/<project-hash>/memory/`. Multiple agents writing concurrently = trampling.

**Convention (week of 2026-05-19):**
- Only the **main coordinator session** (the user's direct Claude Code session) writes new memory notes.
- Sub-agents (wallet, storefronts, docs branches) work strictly from the **existing memory snapshot** and PLAN.md.
- If a sub-agent discovers something memory-worthy, it writes the proposed memory entry into the merge-request body; the main session decides whether to persist it.
- The vendored `agent-memory/` snapshot is re-synced ONLY by the main session via `./agent-memory/sync-from-live.sh`.

This stays under one writer at all times, eliminating the trample.

---

## 5. Pre-merge gate — what `./scripts/pre-merge-gate.sh` enforces

The gate is the contract between an agent claiming "done" and the user accepting "ready to merge". It runs:

1. **`dotnet build PolarSharp.slnx --configuration Release`** — must succeed with 0 warnings, 0 errors.
2. **`dotnet test PolarSharp.slnx --configuration Release --no-build --filter "Category!=Integration"`** — full unit suite must pass.
3. **Lift-shift CI guard** (`./scripts/verify-storefronts-no-polarsharp-deps.sh`) — when present + when the agent's branch touches storefront-core packages, the guard must pass. Wallet lift-shift guard ships with Phase 22.
4. **`docfx build`** when DocFX changed in the branch — broken cross-references must fail the build.
5. **Per-package documentation gate** — every new public type must have inline XML docs (CS1591 is build error already, so step 1 catches this; the gate just confirms).
6. **No leftover dirty state** — the agent must have committed everything; `git status` must be clean.

The gate prints a clear PASS/FAIL summary at the end. Run it BEFORE asking for merge; iterate locally until it passes.

### Per-agent integration tests

The gate runs unit tests only by default (matches the "fast" CI mode). Each agent is ALSO responsible for running their relevant integration tests (`--filter "Category=Integration&Provider=<X>"`) at least once before requesting merge, and noting the result in the merge-request body. Integration tests requiring Docker can't run as part of the gate on every host.

---

## 6. Merge-request format (in the branch's final commit message OR a separate post-to-user)

When an agent is ready for merge, it posts to the user:

```
Branch:           agent/wallet/phase-20-event-store
Phase(s):         Phase 20 (wallet event-sourced core)
Files added:      <count> in src/ + <count> in tests/
Files modified:   <count> (none in LOCKED files except: <list>)
Pre-merge gate:   PASS (timestamp)
Integration tests run locally:
  - dotnet test --filter "Category=Integration&Provider=Wallet" → 12/12 PASS
Memory-worthy discoveries (for main session to consider persisting):
  - <one-line note> OR "none"
Cross-tree implications:
  - <one-line note> OR "none"
Ready to merge.
```

---

## 7. What this week's three agents are doing

| Agent | Phase | Branch | Expected scope |
|---|---|---|---|
| **A — wallet** | Phase 20 (event-sourced wallet core) | `agent/wallet/phase-20-event-store` | `src/PolarSharp.PrepaidWallets.Abstractions` + `src/PolarSharp.PrepaidWallets` + Marten + EF Core common base. Tests for each. Per the PLAN, ~30-50 source files + ~30-50 tests. |
| **B — storefronts** | Phase 25 (storefront core abstractions + cart + checkout services) | `agent/storefronts/phase-25-core-services` | `src/PolarSharp.EcommerceStorefronts` core + cart + checkout services. ~20-40 source files + ~20-30 tests. |
| **C — docs** | Documentation backfill for already-shipped v1.3 features | `agent/docs/v1.3-docfx-sweep` | `docs/articles/*` cross-link fixes, missing narratives, appsettings reference touch-ups. Doc-only. |

All three work this week MUST NOT touch each other's `src/` trees, must not touch each other's `docs/` paths per the matrix in section 2, and must not touch the LOCKED files in section 3 without explicit flagging.

---

## 8. When the friction is felt — escalation path

Sub-agents that find this coordination doctrine blocking them on legitimate work post to the user:

```
COORDINATION ISSUE
What I'm trying to do:    <one line>
What's blocking me:       <which rule, what file>
Suggested workaround:     <option A / option B>
```

The user decides whether to:
- Approve a one-time override (recorded in the merge-request body)
- Update this file for everyone going forward
- Re-scope the agent's task

Friction observations from each agent's first merge-request are valuable data — they tell us which rules need refinement before scaling to 6-7 agents.

---

## 9. Scaling beyond 3 agents

This file is sized for **2-3 concurrent agents**. Scaling to 6-7 (the v1.5 target) requires:

- PLAN.md split into `PLAN/<feature>.md` files referenced from a thin root (~2h)
- Directory.Packages.props sectioned with per-agent reserved version-range blocks (~15min)
- A bot or hook that auto-runs `./scripts/pre-merge-gate.sh` on push (~1h)
- A `/ultrareview` pass mandatory on every branch before merge

Do those AFTER measuring the friction this week's 3 agents actually hit. Don't over-engineer guardrails for problems that haven't surfaced.
