---
name: agentic-master commit requires --no-confirm in non-interactive contexts
description: agentic-master commit (--ai or --manual) prompts y/n by default; always pass --no-confirm when running from a Bash tool that has no stdin
type: feedback
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
`agentic-master commit --ai` and `agentic-master commit --manual "<msg>"` both prompt the user with `Commit with this message? [y/n]` before running `git commit` (line 1409 of `/Users/mollsandhersh/agentic-dev-stack/scripts/agentic-master.sh`). When invoked from a Bash tool call (no interactive stdin), the script hangs forever waiting for input.

**Always add `--no-confirm` when invoking from a Bash tool**:
- `agentic-master commit --manual "<msg>" --no-confirm`
- `agentic-master commit --ai --no-confirm`

**Why:** Wasted ~15 minutes on 2026-05-13 with a hung commit process for the v1.3.H release. The Ollama variant `agentic-master commit --ai` happened to complete because Ollama prints the proposed message and the prompt landed silently, but a re-run with `--manual` blocked indefinitely. Both modes have the same prompt; both need `--no-confirm` from non-interactive contexts.

**How to apply:** every `agentic-master commit` call I make from a Bash tool. The user CAN review the message in the conversation before I invoke the wrapper, so the y/n confirmation is redundant — I draft, they approve in chat, then I commit.

The `--dry-run` flag also exists (line 1399) for "show me what would commit without actually committing"; useful when I want to preview the diff handling without producing a commit.
