# Running Multiple Claude Code Terminals Concurrently

A friendly, step-by-step guide to spawning 2–N Claude Code agents at the same time on this project — each working independently in its own copy of the code, on its own branch, without stepping on the others.

> **Companion docs.**
> This file tells you HOW to spawn agents.
> [`AGENT-COORDINATION.md`](./AGENT-COORDINATION.md) tells you WHAT RULES the agents follow once spawned (file ownership, locked files, merge-request format, etc).
> Read AGENT-COORDINATION.md once to understand the rules; come back here every time you actually spawn agents.

---

## The big picture (in plain language)

Imagine you've hired three workers to renovate three rooms in your house at the same time. If you put all three workers in the same room with the same toolbox, they'd trip over each other, mix up each other's paint, and argue over who gets to use the drill.

**The fix is obvious in real life: give each worker their own room and their own toolbox.** That's exactly what we do for parallel Claude agents:

| Real-world analogy | Claude Code equivalent |
|---|---|
| A separate room for each worker | A separate **git worktree** — a private copy of the entire project on disk |
| Each worker's own notebook | A separate **git branch** — each agent's commits go to their branch only |
| The foreman who reviews + signs off the work | **You** — you manually merge each branch into `main` when an agent reports done |
| Posted house rules (no painting the wall colors until I approve) | [`AGENT-COORDINATION.md`](./AGENT-COORDINATION.md) — the locked-files / file-ownership / merge-request rules every agent reads first |
| The walk-through inspection before signing off | `./scripts/pre-merge-gate.sh` — the script every agent runs before saying "ready to merge" |

That's the whole model. Worktrees + branches + a written rulebook + a pre-merge inspection + manual merges by you. No magic.

---

## Before you spawn anything (one-time per agent wave)

These four prerequisites need to be true. Most are already true from when this doc was set up; you mostly just verify.

### Prereq 1 — You're in the main worktree

```sh
cd /Users/mollsandhersh/Repos/Polar.sh_Nuget
pwd
# Should print: /Users/mollsandhersh/Repos/Polar.sh_Nuget
git status
# Should print: On branch main, working tree clean (or close to it)
```

If the working tree isn't clean, commit or stash before spawning new agents — you want to know exactly what state main is in before forking from it.

### Prereq 2 — `main` is up to date with GitHub

```sh
git fetch origin
git status
# Should say: Your branch is up to date with 'origin/main'.
```

If you're behind, `git pull` first. Agents fork off your local `main`; if local is stale, they start from stale code.

### Prereq 3 — Decide what each agent will do

Open [`AGENT-COORDINATION.md`](./AGENT-COORDINATION.md) §2 — the file-ownership matrix. Make sure each new agent has:

- A defined `src/` subtree they exclusively own (or "none" for doc-only agents)
- A defined `tests/` subtree they exclusively own
- A defined `docs/` path they exclusively own
- A defined PLAN.md section they may edit

If two agents you're about to spawn would touch the same subtree, **don't spawn them at the same time** — split into two waves. (Or update the matrix to assign one of them a different subtree.)

### Prereq 4 — Update AGENT-COORDINATION.md §7 for THIS WAVE

The "this week's agents" table in section 7 should list every active agent. Add rows for the new agents you're about to spawn so you (and they) can see who's doing what at a glance. This is also where the user finds the merge order when work comes back.

---

## The 6-step process for spawning N agents

(Replace `N` with however many you want. We've successfully run 3 concurrently; AGENT-COORDINATION.md §9 has the prerequisites you need before scaling to 6–7.)

### Step 1 — Pick branch names

Naming convention from AGENT-COORDINATION.md §1:

```
agent/<feature-area>/<phase-or-task-id>-<short-slug>
```

Examples that worked:
- `agent/wallet/phase-20-event-store`
- `agent/storefronts/phase-25-core-services`
- `agent/docs/v1.3-docfx-sweep`

Pick one per agent. Write them down before you start typing.

### Step 2 — Create one worktree per agent

For each agent, from the main worktree, run:

```sh
git worktree add ../Polar.sh_Nuget.<short-name> -b <branch-name-from-step-1> main
```

Real example (the three agents currently running):

```sh
git worktree add ../Polar.sh_Nuget.wallet      -b agent/wallet/phase-20-event-store      main
git worktree add ../Polar.sh_Nuget.storefronts -b agent/storefronts/phase-25-core-services main
git worktree add ../Polar.sh_Nuget.docs        -b agent/docs/v1.3-docfx-sweep            main
```

Each command does TWO things at once:
1. Creates a sibling directory (e.g. `../Polar.sh_Nuget.wallet`) with a complete checked-out copy of the project
2. Creates and switches that copy onto a new branch (the `-b agent/...` part)

After all your worktree-add commands, verify:

```sh
git worktree list
```

You should see one line per worktree, each on its own branch:

```
/Users/mollsandhersh/Repos/Polar.sh_Nuget              <commit-hash> [main]
/Users/mollsandhersh/Repos/Polar.sh_Nuget.wallet       <commit-hash> [agent/wallet/phase-20-event-store]
/Users/mollsandhersh/Repos/Polar.sh_Nuget.storefronts  <commit-hash> [agent/storefronts/phase-25-core-services]
/Users/mollsandhersh/Repos/Polar.sh_Nuget.docs         <commit-hash> [agent/docs/v1.3-docfx-sweep]
```

> **Why sibling directories instead of subdirectories?**
> Git worktrees are conventionally placed next to the main directory, not inside it, so they don't get confused for ignored subdirs and so tools like `find` and `grep` from the main worktree don't accidentally walk into them.

### Step 3 — Write one prompt file per agent

Prompts live in `.agent-prompts/` (gitignored — these are operational artifacts, not source code).

```sh
mkdir -p /Users/mollsandhersh/Repos/Polar.sh_Nuget/.agent-prompts
```

For each agent, create `.agent-prompts/<short-name>.md` with the prompt content. The three prompts from the first wave (`wallet.md`, `storefronts.md`, `docs.md`) are excellent templates — copy one and adapt the scope/file-ownership/deliverables sections.

**A good agent prompt has these sections** (in this order, all required):

1. **Identity + mission** — one sentence: who are you, what are you shipping?
2. **You're running in a worktree at X, on branch Y** — orients the agent so it doesn't try to create a branch (the worktree-add already did that)
3. **Project context** — 2–4 paragraphs explaining what PolarSharp is, the lift-shift discipline, that other agents are working in parallel and your scope doesn't overlap theirs
4. **MANDATORY pre-work reading** — numbered list of files the agent MUST read before writing any code (CLAUDE.md, AGENT-COORDINATION.md, the relevant Case Study files, the PLAN.md section, ZoranHorvat.md). This is the single most important section — without it, the agent will violate project conventions.
5. **Your scope (exclusive)** — exactly which subtrees the agent owns. Phrase it as "you own X" and "you may NOT touch Y". Be specific.
6. **Hard constraints** — lift-shift discipline, documentation requirements (CS1591 build-error, per-package README, DocFX article, Implementation Narrative), tests at ≥90% coverage, AOT-safe, pre-merge gate must pass, memory-write rule, Conventional Commits style
7. **Workflow** — "you're already on branch X; commit incrementally; when done, `git push -u origin <branch>`; DO NOT merge to main"
8. **Deliverables** — concrete counts (e.g. "~30–50 source files + ~30–50 tests + 1 DocFX article + 1 Narrative + per-package READMEs")
9. **Merge-request body template** — verbatim from AGENT-COORDINATION.md §6, so the agent reports back in the exact format you want

Look at `.agent-prompts/wallet.md` for a complete worked example. Copy it, swap the phase, swap the scope, swap the deliverables, and you've got your new agent prompt.

### Step 4 — Open one terminal per agent

This is the actual "go" moment. Open as many terminal windows or tabs as you have agents. In each one:

```sh
# Substitute the right <name> per agent:
pbcopy < /Users/mollsandhersh/Repos/Polar.sh_Nuget/.agent-prompts/<name>.md
cd /Users/mollsandhersh/Repos/Polar.sh_Nuget.<name>
direnv allow                 # first time only per worktree; loads GH_TOKEN, POLAR_SANDBOX_TOKEN, etc.
claude
```

The `pbcopy` command loads the prompt into your clipboard. Then in Claude:

1. Press `Cmd+V` to paste the prompt
2. Press `Enter` to send it

That's it. The agent reads the prompt, reads its mandatory pre-work files, and starts working.

> **Why `direnv allow`?**
> direnv tracks "this `.envrc` is approved" per filesystem path. Each new worktree is a new path direnv has never seen, so it refuses to load secrets until you explicitly approve. One `direnv allow` per worktree, once, forever.
>
> If you skip it, the agent's `git push`, `gh` commands, and any test that needs `POLAR_SANDBOX_TOKEN` will fail mysteriously with "no token found." Always do `direnv allow`.

### Step 5 — Let them work; check in periodically

Each agent runs autonomously. Don't watch every step.

**What to watch for in each terminal:**

- **Agent asks a question** — usually about an ambiguity in the prompt. Answer it, or send it back to the prompt section that covers the situation.
- **Agent reports a blocker** — per AGENT-COORDINATION.md §8, the agent will say `COORDINATION ISSUE` followed by what's blocking. Decide: one-time override, update the rules for everyone, or re-scope the task.
- **Agent reports `Ready to merge`** — see Step 6 below.

**Realistic timing** (rough; varies by phase scope):
- Doc-only agent (audit + 2–3 narratives + cross-link fixes): 1–3 hours
- Feature agent (30–50 src files + tests + docs): 4–10 hours of agent work; longer wall-clock if you're not actively in the terminal

**You can absolutely walk away** between checkpoints. The agents are checkpointing their work via Conventional Commits as they go. Even if a terminal closes accidentally, the committed work is preserved on the branch.

### Step 6 — When an agent reports `Ready to merge`

The agent's final message will include a merge-request body in this format (per AGENT-COORDINATION.md §6):

```
Branch:           agent/wallet/phase-20-event-store
Phase(s):         Phase 20 (wallet event-sourced core)
Files added:      35 in src/ + 42 in tests/ + 2 in docs/
Files modified:   0 (LOCKED files touched: "none")
Pre-merge gate:   PASS (2026-05-21 14:32)
Integration tests run locally: n/a — Phase 21 covers that
Memory-worthy discoveries: none
Cross-tree implications: none
LOCKED file edits needed by user: none
Worktree path:    /Users/mollsandhersh/Repos/Polar.sh_Nuget.wallet
Ready to merge.
```

When you get one of these:

#### Step 6a — Verify the gate independently (paranoid mode)

The agent says it ran the pre-merge gate. Trust but verify:

```sh
cd /Users/mollsandhersh/Repos/Polar.sh_Nuget.<name>
./scripts/pre-merge-gate.sh
```

If your re-run passes too, the agent told the truth. Proceed.

#### Step 6b — Inspect the diff

```sh
cd /Users/mollsandhersh/Repos/Polar.sh_Nuget   # back to main worktree
git fetch origin
git log --oneline main..origin/<branch>          # see all commits
git diff main..origin/<branch> --stat            # see all files changed
git diff main..origin/<branch>                   # see all changes (long)
```

Skim. Pay particular attention to:
- Anything in a LOCKED file (`Directory.Packages.props`, `PolarSharp.slnx`, `CLAUDE.md`, `README.md`, `Directory.Build.props`) — the agent should NOT have edited these unless explicitly flagged in the merge-request body
- New `<PackageReference>` entries to packages that aren't in `Directory.Packages.props` (these will fail the central-package-management build; the agent should have noted them for you to add)
- Anything outside the agent's owned scope per AGENT-COORDINATION.md §2

If you find a problem, ask the agent to fix it on its branch before merging.

#### Step 6c — Land any LOCKED-file edits the agent requested

If the merge-request body says `LOCKED file edits needed by user: <list>`, do those edits on `main` FIRST, push them, then merge the agent's branch. This avoids the agent having to rebase.

Example:
```sh
# Agent requested adding the new test project to slnx
# Open PolarSharp.slnx, add the new <Project Path="..."/> line, save
git add PolarSharp.slnx
git commit -m "chore(slnx): register PolarSharp.PrepaidWallets.Tests for upcoming wallet merge"
git push
```

#### Step 6d — Merge

The recommended merge style is `--no-ff` so the merge commit clearly captures the agent's branch as a unit:

```sh
git checkout main
git pull origin main                                              # get any LOCKED-file edits you just made
git merge --no-ff origin/<branch> -m "Merge agent/<branch>: <one-line description>"
git push origin main
```

#### Step 6e — Re-run the gate on main to confirm nothing broke

```sh
./scripts/pre-merge-gate.sh
```

If green, the merge is clean. If red, something integrated badly — investigate.

### Step 7 — Recommended merge order when multiple agents finish

Per AGENT-COORDINATION.md §1, the agents work on disjoint trees, so order theoretically doesn't matter. But practically:

1. **Docs branches first** — lowest risk, doc-only, can't break code
2. **Feature branches second** — usually no LOCKED-file edits needed
3. **Feature branches with LOCKED-file edit requests last** — you do their requested `Directory.Packages.props` / `slnx` additions on main first, then merge

If any two feature branches touched the same package (a violation of the file-ownership matrix), merge the smaller one first to surface conflicts early.

---

## Cleanup after a merge

Once an agent's work is merged into main on GitHub, the worktree and branch are no longer needed.

```sh
# Remove the worktree (deletes the sibling directory):
git worktree remove ../Polar.sh_Nuget.<name>

# Delete the local branch:
git branch -d agent/<area>/<slug>

# Delete the remote branch (cleans up GitHub's branch list):
git push origin --delete agent/<area>/<slug>
```

> **Don't delete the worktree before the merge is on GitHub.**
> The worktree is the only copy of any uncommitted state. If the agent crashed mid-work and you removed the worktree without verifying GitHub has the commits, you've lost work.

If you're keeping the worktree around for a follow-up wave (e.g. wallet Phase 20 → Phase 21), you can skip the worktree removal and reuse the same directory. Just `git checkout main && git pull && git checkout -b agent/wallet/phase-21-...` inside it.

---

## Common gotchas

### `direnv: error /Users/.../...envrc is blocked`

**Fix**: `cd` into the worktree and run `direnv allow`. One-time per worktree. See Step 4.

### `fatal: '<branch>' is already checked out at '...'`

You tried to check out a branch that's checked out in another worktree. Git doesn't allow the same branch in two places.

**Fix**: either (a) work in the existing worktree, or (b) use a different branch name.

### Agent's commits don't appear when you `git log` in the main worktree

The agent committed to its branch in its worktree. From your main worktree (which is on `main`), the agent's commits exist only on the agent's branch. To see them:

```sh
git log <agent-branch>
# OR after the agent pushed:
git fetch origin
git log origin/<agent-branch>
```

### Agent edited a file you own in the main worktree

This means the file-ownership matrix in AGENT-COORDINATION.md §2 wasn't honored. Two options:
1. Reject the merge; ask the agent to revert that file
2. Accept it as a one-time override and document why in the merge commit message

Frequency of this happening is the signal: rare = exception, frequent = the matrix needs updating before scaling up.

### Pre-merge gate fails on `git status` not clean

Agent forgot to commit something. Either ask the agent to commit it, or check what's left over — sometimes it's bin/obj output that should have been ignored.

### Pre-merge gate fails on DocFX broken cross-reference

A `<see cref="...">` in the agent's new XML doc points at a type that doesn't exist (typo) or that wasn't part of the same merge wave (drift). Fix the cref or ask the agent to.

### Two agents both requested edits to Directory.Packages.props

Combine their requests, do them as a single commit on main, push, then merge the agents in order.

### An agent's worktree fills up your disk (bin/obj/ artifacts)

Each worktree's `bin/` and `obj/` directories are independent — N worktrees can take N× the build artifact space. Periodically:

```sh
cd <worktree>
dotnet clean
# OR more aggressively:
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
```

(This is safe — bin/obj are gitignored and regenerable.)

---

## When to scale up beyond 3 agents

AGENT-COORDINATION.md §9 lists the prerequisites for 6–7 agents (`PLAN.md` split into per-feature includes, sectioned `Directory.Packages.props`, mandatory `/ultrareview` per branch, auto-running pre-merge gate). Don't scale up before those are in place.

A clean signal that you're ready: **two or three concurrent runs land cleanly with no LOCKED-file conflicts and no rule violations**. That's the system working as designed; scaling adds the same dynamics N more times.

A clean signal that you're NOT ready: **agents kept hitting the same coordination issue** (e.g. they all wanted to add packages to `Directory.Packages.props`). Fix the friction first.

---

## Quick reference card — the whole process on one screen

```sh
# ── 1. Prereqs (in main worktree) ─────────────────────────────────────────────
cd /Users/mollsandhersh/Repos/Polar.sh_Nuget
git status                                    # working tree clean?
git fetch origin && git status                # main up to date with origin?
# Read AGENT-COORDINATION.md §2 — pick non-overlapping scopes
# Edit AGENT-COORDINATION.md §7 — list this wave's agents

# ── 2. Create worktrees (one per agent) ──────────────────────────────────────
git worktree add ../Polar.sh_Nuget.<name> -b agent/<area>/<slug> main
# (repeat per agent)
git worktree list                             # verify

# ── 3. Write prompts ──────────────────────────────────────────────────────────
# Copy .agent-prompts/wallet.md as template; adapt scope + deliverables
# Save to .agent-prompts/<name>.md per agent

# ── 4. For each agent, in a new terminal: ─────────────────────────────────────
pbcopy < /Users/mollsandhersh/Repos/Polar.sh_Nuget/.agent-prompts/<name>.md
cd /Users/mollsandhersh/Repos/Polar.sh_Nuget.<name>
direnv allow                                  # first time per worktree
claude
# Then in Claude: Cmd+V, Enter

# ── 5. Periodic check-in ──────────────────────────────────────────────────────
# Watch each terminal for questions, blockers, "Ready to merge"

# ── 6. On "Ready to merge" ────────────────────────────────────────────────────
cd /Users/mollsandhersh/Repos/Polar.sh_Nuget.<name>
./scripts/pre-merge-gate.sh                   # verify the gate passes
cd /Users/mollsandhersh/Repos/Polar.sh_Nuget
git fetch origin
git diff main..origin/<branch> --stat        # inspect
# (do any LOCKED-file edits the agent requested, push them)
git checkout main
git pull origin main
git merge --no-ff origin/<branch> -m "Merge agent/<branch>: <description>"
git push origin main
./scripts/pre-merge-gate.sh                   # confirm nothing broke

# ── 7. Cleanup ────────────────────────────────────────────────────────────────
git worktree remove ../Polar.sh_Nuget.<name>
git branch -d agent/<area>/<slug>
git push origin --delete agent/<area>/<slug>
```

---

## TL;DR

1. **Each agent gets its own worktree** (its own room with its own toolbox)
2. **Each agent works on its own branch** (its own notebook)
3. **You write a prompt file per agent** in `.agent-prompts/<name>.md`
4. **You open one terminal per agent**: `pbcopy` the prompt, `cd` into the worktree, `direnv allow`, `claude`, paste, Enter
5. **Agents work autonomously, commit incrementally, push when done**
6. **You manually merge each branch into `main`** after inspecting the diff and re-running the pre-merge gate
7. **Then you clean up the worktree + delete the branch**

The rules each agent follows are in [`AGENT-COORDINATION.md`](./AGENT-COORDINATION.md). The pre-merge inspection is in [`scripts/pre-merge-gate.sh`](./scripts/pre-merge-gate.sh). The prompt templates are in `.agent-prompts/` (gitignored).

That's the whole system. Now go run more agents.
