---
name: Rotate secrets via pbpaste, never paste into chat
description: For any secret rotation in this repo (Polar OAT, gh tokens, AI provider keys, signing certs), the default pattern is `! pbpaste | gh secret set NAME --repo OWNER/REPO` — secret never enters my conversation context
type: feedback
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
**Pattern:** when the user needs to rotate or set a GitHub Actions repository secret, instruct them to copy the new value to their clipboard and run from their own terminal:

```
! pbpaste | gh secret set <SECRET_NAME> --repo MollsAndHersh/Polar.sh_Nuget
```

After they say "done", I verify by running `gh secret list --repo MollsAndHersh/Polar.sh_Nuget` (which shows the secret name + updated timestamp but NEVER the value), then offer to clear their clipboard with `pbcopy < /dev/null`.

**Why:** 2026-05-13 the user pasted a Polar OAT (`polar_oat_VXOdb...`) directly into the conversation so I could `gh secret set` it for them. The value landed in the transcript permanently. We immediately rotated it via the pbpaste pattern, which kept the new token out of every chat layer. The user explicitly endorsed this as the default going forward.

**How to apply:**
- Default offer for any "I need to set a CI secret" or "rotate this token" request: present BOTH paths but lead with pbpaste, mark direct-paste as the less-safe option.
- Same pattern works for Linux (`xclip -selection clipboard -o`) and Windows PowerShell (`Get-Clipboard`); macOS uses `pbpaste`. This repo's owner is on macOS (Darwin), so `pbpaste` is the correct default.
- After verification, ALWAYS offer to clear the clipboard. It's a 1-line fix that closes one more exposure surface.
- The same hygiene applies to .env files: prefer `gh auth token | tee -a .env >/dev/null` (writes through a pipe, no echo) over interpolating into a heredoc that prints to terminal.

**Anti-pattern to avoid:** asking the user to paste a raw secret into the conversation as a message body. Even when the secret value is "low risk" (sandbox token, dev key), the transcript is permanent and may sync to logs / training data / Anthropic systems / future shared sessions. Default-secure: never let the secret enter chat in the first place.
