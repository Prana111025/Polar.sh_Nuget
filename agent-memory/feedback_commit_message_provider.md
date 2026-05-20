---
name: Commit message provider preference
description: For Polar.sh_Nuget, draft commit messages with Anthropic (Claude) — never Ollama via agentic-master's default
type: feedback
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
For this project (`/Users/mollsandhersh/Repos/Polar.sh_Nuget`), commit messages should be drafted by Claude (Anthropic), not by the local Ollama provider that `agentic-master commit --ai` defaults to when `CLAW_PROVIDER=ollama` is set in the shell.

**Why:** Project owner explicitly stated 2026-05-13 "For this project, I think I want to solely stick to using Anthropic and not use Ollama (via agentic-master)." Quality of commit messages on large diffs matters — Ollama-generated messages tend to skip nuance (audits, design decisions, production-readiness findings) that Claude can surface. The project's strategic preference is Anthropic for AI tasks on this repo.

**How to apply:**
- For commits in this repo, default path: I (Claude) draft the message in conversation, then invoke `agentic-master commit --manual "<message>"` with that message.
- Do NOT invoke `agentic-master commit --ai` unless `CLAW_PROVIDER=anthropic` (with API key) is explicitly set first.
- This applies to every commit in this repo, not just release commits.
- The `CLAW_PROVIDER=ollama` shell default is a machine-level configuration that may be set for other projects; the override is per-project (this one): Anthropic only.

**Provider flag note (2026-05-14 correction):** `agentic-master commit --ai --provider <name>` only accepts the literal values `ollama`, `claw`, or `opencode` — there is NO `--provider anthropic` flag, and `--provider claw` fails because the `claw` binary on this machine is exposed as a shell alias (`rusty-claude-cli`) that `agentic-master` cannot resolve, and `--provider opencode` returned an empty message in practice. So `--manual` with a Claude-drafted message is the ONLY path that honors the Anthropic-only rule reliably. Do not waste turns trying `--provider anthropic` or `--provider claw` — go straight to `--manual` with a HEREDOC-quoted Claude-drafted message.
