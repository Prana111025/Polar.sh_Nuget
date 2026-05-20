---
name: Do not use agentic-master in this project
description: For Polar.sh_Nuget, never invoke agentic-master for commits, push, or any other workflow step — use raw git directly
type: feedback
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---

For this project (`/Users/mollsandhersh/Repos/Polar.sh_Nuget`), do NOT invoke `agentic-master` for any operation — not for `commit --manual`, not for `commit --ai`, not for `push`, not for any other subcommand. Use raw `git` directly instead.

**Why:** Project owner explicitly stated on 2026-05-14 "I want you to NOT use agentic-master for anything in this current project moving forward." This supersedes the standing CLAUDE.md / AGENTS.md "use agentic-master wrappers" rule for THIS project specifically. The wrapper has produced repeated failures on this repo (Ollama default needing override, `--provider claw` failing because the binary is aliased, `--provider opencode` returning empty messages, `--manual` invocations writing zero bytes to stdout) — every commit attempt loses time to wrapper plumbing instead of doing the work.

**How to apply:**
- Use `git add`, `git commit -m "..."`, `git push`, `git status`, `git diff` directly via the Bash tool. Follow the standard CLAUDE.md HEREDOC pattern for multi-line commit messages.
- Still honor every other commit-discipline rule: Claude (Anthropic) drafts the message; the commit trailer is `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`; stage specific files (not `git add -A`).
- This rule also supersedes the older `feedback_commit_message_provider.md` and `feedback_agentic_master_no_confirm.md` notes for this project — they remain useful as historical context about WHY this rule exists, but the operational instruction here is the one to follow.
- Applies project-wide: every commit, every push, every git operation in `/Users/mollsandhersh/Repos/Polar.sh_Nuget`.
