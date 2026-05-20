# agent-context/ — vendored snapshots of home-rooted Claude files

This folder contains snapshots of the **home-rooted** files that PolarSharp's
session bootstrap requires. These files normally live in `$HOME` and apply
across ALL Claude projects on the machine — not just PolarSharp. They're
vendored here so that a `git clone` of this repo on a fresh machine can
install them in one command.

## Files in this directory

| File | Installs to | What it is |
|---|---|---|
| `AGENTS.md` | `$HOME/AGENTS.md` | Universal cross-project workflow rules + agentic-master policy + RAG policy |
| `CLAUDE.home.md` | `$HOME/CLAUDE.md` | Home-level standing requirements (named `.home.md` here to avoid colliding with this project's own `CLAUDE.md` at the repo root) |
| `ZoranHorvat.md` | `$HOME/ZoranHorvat.md` | .NET / C# / Blazor / Telerik / ServiceStack coding standards |
| `install.sh` | (executes; copies the above) | The bootstrap script |

## Installing on a fresh machine

After `git clone` of this repo:

```sh
cd Polar.sh_Nuget
./agent-context/install.sh
```

The script:
- Backs up any existing `$HOME/AGENTS.md` / `$HOME/CLAUDE.md` / `$HOME/ZoranHorvat.md` to a timestamped `$HOME/.agent-context-backup-YYYYMMDD-HHMMSS/` directory
- Prompts before overwriting
- Copies the vendored snapshots to their home-dir destinations
- Preserves file timestamps + modes

## Syncing updates back to the vendored snapshots

When you edit the home-dir versions (e.g. you update `$HOME/AGENTS.md` for a new cross-project policy), the vendored snapshots here go stale. To re-sync them back:

```sh
cp ~/AGENTS.md       agent-context/AGENTS.md
cp ~/CLAUDE.md       agent-context/CLAUDE.home.md
cp ~/ZoranHorvat.md  agent-context/ZoranHorvat.md
```

Then `git add agent-context/ && git commit && git push` so the snapshots are up to date for the next fresh-machine clone.

**TODO future:** automate the sync-back step via a small `sync-from-home.sh` script or a git pre-commit hook. Defer until you actually feel the friction of manual sync.

## Long-term: promote to a separate `~/claude-context/` repo

The current vendor-in-each-project pattern works well when you have one or two Claude projects. Once you accumulate more, the right refactor is to:

1. Create a separate `~/claude-context/` git repo containing the three files
2. Replace each project's `agent-context/install.sh` with a one-liner that clones (or `git pull`s) `claude-context` into `$HOME`
3. Cross-project policy updates then propagate to all projects via `git pull` in `~/claude-context/` — no per-project re-vendoring needed

Defer until the friction is real. The vendor-in-project pattern is fine for now.

## Why these files aren't in the project root

These files are **cross-project** by design — they apply to any Claude project on this machine, not just PolarSharp. Putting them at home-dir paths means a single edit propagates to all projects. The `agent-context/` snapshot here is the replication mechanism that fills the gap on fresh-clone scenarios, but the canonical source-of-truth remains the home-dir copy.

See also: project root `CLAUDE.md` "Required Reading Order" section + the migration note at the end of that section.
