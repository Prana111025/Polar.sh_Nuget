# agent-memory/ — vendored snapshots of Claude's per-project memory notes

This folder contains snapshots of Claude Code's persistent memory notes for THIS project. The notes capture project-specific behavioral rules, architectural anchors, user-feedback patterns, and lessons learned across sessions — context that took multiple sessions to build up and that's essential for productive work.

## Why this exists

In normal operation, Claude writes memory notes to:

```
$HOME/.claude/projects/{project-hash}/memory/
```

That directory is **machine-local** and excluded from git (the project `.gitignore` excludes `.claude/`). Without vendoring, a fresh `git clone` on a different machine produces a session that has NO memory context — the agent has to re-derive every architectural anchor and behavioral rule from PLAN.md prose. Memory notes for this project currently include (per the index in `MEMORY.md`):

- **`project_agent_driven_marketplaces.md`** — the army-of-marketplaces deployment model + agent-driven WC selection (mission-critical context)
- **`project_tenant_sso_per_provider.md`** — per-tenant SSO architecture (BYOK + per-provider packages)
- **`feedback_tenant_isolation_every_new_entity.md`** — the 5-layer tenant-isolation acceptance criterion
- **`feedback_verify_dotnet_capabilities.md`** — verify .NET capabilities via WebSearch (the passkey-mistake correction)
- **`feedback_live_polar_tests_required.md`** — every IPolarXxxApi wrapper needs a live-sandbox test
- ...and more (see `MEMORY.md`)

These notes are far too valuable to lose on a fresh-clone.

## Installing on a fresh machine

After `git clone` of this repo:

```sh
cd Polar.sh_Nuget
./agent-memory/install.sh
```

The script:
- Computes the correct Claude-Code project-hash from the current working directory (works regardless of where you cloned the project — `~/dev/`, `~/projects/`, `~/Repos/`, etc.)
- Creates `$HOME/.claude/projects/{computed-hash}/memory/` if missing
- Backs up any existing memory files at that location to a timestamped `$HOME/.agent-memory-backup-YYYYMMDD-HHMMSS/` directory before overwriting
- Copies every `.md` file from this folder to the destination (except this README)
- Preserves file timestamps + modes

The next Claude session on that machine then bootstraps with full memory context — same architectural anchors + behavioral rules as on your original machine.

## Syncing live memory back to the vendored snapshot

When Claude writes new memory notes during a session (or edits existing ones), the writes land in the live `~/.claude/projects/{hash}/memory/` directory. The vendored snapshot in THIS folder goes stale.

To re-sync:

```sh
./agent-memory/sync-from-live.sh
```

The script:
- Removes the existing vendored `.md` files (except this README) so deleted memory notes are reflected
- Copies the current state of the live memory dir back to this folder
- Prints `git status` instructions for committing the updated snapshot

Then:

```sh
git add agent-memory/
git commit -m "chore: sync agent-memory snapshot"
git push
```

The next fresh-machine clone will get the updated memory context.

**Tip:** consider running `sync-from-live.sh` at the end of any session that wrote new memory notes — keep the vendored snapshot fresh so cross-machine continuity stays cheap.

## Privacy note

Memory notes may capture user feedback verbatim — including off-the-cuff comments about other tools, deferred work, or known bugs. For a **private repo** (which this is), the privacy stance is "anything I'm comfortable telling future-me or a contributor". For a **public repo**, scrub this directory before push — strip user feedback that could be embarrassing or mention third-party tools/services in unflattering ways.

## Format note

Each memory `.md` file has YAML frontmatter (`name`, `description`, `type`) followed by markdown content. The format is fully documented in `LARGE-PROJECT-BEST-PRACTICES.md` ("Template 4: Memory note frontmatter"). Edits are safe to make directly to the vendored snapshots if you prefer; the `install.sh` will pick them up on the next install.

## Long-term refactor (deferred)

As with `agent-context/`, the vendor-in-each-project pattern works fine for one or two Claude projects. Once you have more, consider whether the per-project nature of memory still requires vendoring — memory genuinely IS per-project, so there's no consolidation gain (unlike `agent-context/` which has cross-project shared content). The vendor pattern stays load-bearing here.

The one possible refactor: a git pre-commit hook that auto-runs `sync-from-live.sh` so the snapshot is always up to date without manual intervention. Defer until manual-sync friction is real.
