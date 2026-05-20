# AGENTS.md

Universal source of truth for agentic work across any project on this machine.

Read in this order:
1. AGENTS.md (this file; home-rooted; universal across all projects)
2. PLAN.md (project-rooted; the current project's plan)
3. TASKS.md (project-rooted; the current project's tasks)
4. PROGRESS.md (project-rooted; the current project's progress log)
5. DECISIONS.md (project-rooted; the current project's locked decisions)
6. ZoranHorvat.md (home-rooted; only for .NET/C#/Blazor/Telerik/ServiceStack work)

**Note on locations (clarified 2026-05-19):** Items 2–5 live in the **project's own folder**, NOT in the home directory. Each Claude project on this machine has its own PLAN/TASKS/PROGRESS/DECISIONS. Earlier convention had them home-rooted; that was a single-project-era design that didn't scale to multiple Claude projects. Items 1 + 6 remain home-rooted because they apply universally across projects.

## Core Rules

- Plan before editing.
- Use project-local RAG context before answering architecture/codebase questions.
- Do not overwrite user work without explicit approval.
- Use task branches for agent work.
- Commit only after verification succeeds or an explicit human override is recorded.

## Agile Planning Template

Every non-trivial feature/refactor plan must use this structure:

### User Story
As a [role], I want [capability], so that [benefit].

### Acceptance Criteria
- [ ] Verifiable criterion 1
- [ ] Verifiable criterion 2
- [ ] Edge cases documented

### Technical Tasks
1. Task name — Owner: [agent/tool] — Dependencies: [none/task ids]
2. Task name — Owner: [agent/tool] — Dependencies: [none/task ids]

### Style and Architecture Gates
- Apply ZoranHorvat.md only for .NET/C#/Blazor/Telerik/ServiceStack work.
- Apply frontend-specific rules only for frontend-only tasks.

### Verification Steps
- Build command(s)
- Test command(s)
- RAG/source references used
- Manual review checkpoints

## Git and GitHub Command Policy — REQUIRED

All agents and orchestration tools MUST use `agentic-master` for Git and GitHub operations. Do not run mutating Git/GitHub commands directly unless the human user explicitly overrides this policy.

Do NOT run these mutating commands directly:

- `git commit`
- `git push`
- `git pull`
- `git merge`
- `git rebase`
- `git checkout -b`
- `git switch -c`
- `git reset`
- `git revert`
- `git stash`
- `git clean`
- `gh pr create`
- `gh workflow run`

Use these instead:

- `agentic-master new-task <TASK-ID>`
- `agentic-master commit --ai`
- `agentic-master commit --ai --provider ollama --model <MODEL_NAME>`
- `agentic-master commit --ai --provider claw`
- `agentic-master commit --ai --provider opencode`
- `agentic-master commit --ai --dry-run`
- `agentic-master commit --ai --no-confirm`
- `agentic-master commit --manual "<message>"`
- `agentic-master push`
- `agentic-master pull`
- `agentic-master finish-task`
- `agentic-master branch`
- `agentic-master checkout <branch-or-ref>`
- `agentic-master checkout --create <new-branch>`
- `agentic-master switch <branch>`
- `agentic-master switch --create <new-branch>`
- `agentic-master merge <source-branch-or-ref> [--no-ff]`
- `agentic-master rebase <upstream> [--force]`
- `agentic-master revert <commit-or-ref> [--no-commit] [--force]`
- `agentic-master reset <ref> [--soft|--mixed|--hard] [--force]`
- `agentic-master stash [--message "text"]`
- `agentic-master stash-pop [stash-ref]`
- `agentic-master clean --dry-run`
- `agentic-master clean --force`
- `agentic-master log [count]`
- `agentic-master verify`
- `agentic-master detect-conflicts`

Reason: `agentic-master` keeps repository metadata, task state, verification timestamps, GitHub Actions awareness, and safe commit workflows synchronized.

## RAG Policy

Use project-local RAG before answering repo-specific questions.

- Code indexing uses `agentic-master index` or `index-repo .`.
- Ad hoc document ingestion uses `.agentic/rag/inbox`.
- Successful watcher ingestion moves files to `.agentic/rag/archive`.
- Failed watcher ingestion moves files to `.agentic/rag/error`.

## .NET/C# Zoran Horvat Routing

For .NET/C#, `.cs`, `.razor`, `.csproj`, Blazor, Telerik, or ServiceStack tasks, read and apply `ZoranHorvat.md`. For frontend-only TypeScript/React/Vue/Svelte/Astro/Angular/Tailwind/HTML/CSS/SCSS tasks, do not apply `ZoranHorvat.md` unless the task explicitly touches .NET/Blazor/Telerik.
