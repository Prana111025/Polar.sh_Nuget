---
name: Never echo secrets even in length/existence checks
description: When verifying an env var is set, use ${VAR:+yes}/${#VAR} ONLY — never include the bare variable in any output, even as a fallback branch
type: feedback
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
**Rule:** when checking whether a secret env var (GH_TOKEN, ANTHROPIC_API_KEY, webhook signing keys, etc.) is set, never include the variable's value in any output — even as the fallback branch of an existence check.

**Why:** 2026-05-13 I wrote `echo "GH_TOKEN in env: ${GH_TOKEN:+yes (length ${#GH_TOKEN})}${GH_TOKEN:-no}"`. The intent was "say yes-with-length when set, no when unset." But the trailing `${GH_TOKEN:-no}` ALSO expands when GH_TOKEN is set (because `:-` means "substitute default ONLY if unset/null") — so it printed the full token value into the conversation. The user had to rotate the token. Tokens in conversation transcripts cannot be retracted.

**Wrong:**
```bash
echo "${VAR:+set}${VAR:-unset}"          # Bash expands the unset-branch too when set — leaks value
echo "VAR=$VAR"                          # Obvious leak
echo "got token: $TOKEN"                 # Obvious leak
```

**Right:**
```bash
[[ -n "$VAR" ]] && echo "set (${#VAR} chars)" || echo "unset"   # Branches at the SHELL level, not via parameter expansion
echo "VAR ${VAR:+is set}${VAR:-is unset}"                         # Both branches still parameter expansion — STILL WRONG; same trap
test -n "$VAR" && echo "yes" || echo "no"                         # Safest
```

**How to apply:** any time a secret env var appears in a Bash tool command, the command must NOT include `$VAR` or `${VAR}` anywhere in the printed output — only via `${#VAR}` (length only) or `${VAR:+marker}` alone (with no companion fallback expression). When in doubt, branch with `test`/`[[`/`if` at the shell statement level instead of inside a single `echo` argument.

Also applies to log output, error messages, and `set -x` traces — `set +x` before any line that references a secret.
