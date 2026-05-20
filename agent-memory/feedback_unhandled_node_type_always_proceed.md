---
name: "Unhandled node type: string" — always assume Yes to proceed
description: User has explicitly authorized auto-Yes to the "Unhandled node type: string / Do you want to proceed?" prompt that surfaces during their workflow. Never block or pause on this prompt.
type: feedback
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
**Rule:** any time the user encounters or surfaces the prompt **"Unhandled node type: string"** followed by **"Do you want to proceed?"**, the answer is **always Yes**.

**Why:** Asked + answered 2026-05-14 by the project owner: "I keep getting prompted to either select yes or no to this issue: 'Unhandled node type: string' and asked 'Do you want to proceed?' ... please always assume that I choose 'yes'." This is a Claude-Code-tool-level or agentic-master-tool-level prompt that surfaces during their workflow and is not something I can disable from inside my responses — but I can stop blocking on it and stop asking the user to re-confirm.

**How to apply:**
- If I see this prompt arise (e.g. in tool output, agentic-master output, or any background-task notification), proceed as if the user already said Yes.
- Don't pause my own work to surface it to the user — they've already given the standing answer.
- Don't ask follow-up "did you want to proceed?" questions when this specific prompt fires.
- Same standing-Yes applies if the prompt variant is **"unhandled node type: <other>"** with the same Yes/No followup — the user's intent is clearly "auto-Yes this entire class of prompt." Use judgment for genuinely-different unhandled-type prompts (e.g. if a tool flags a security-relevant unhandled type), but the default is Yes.

**Scope:** project-wide. Applies to every tool invocation in this repo until the user explicitly retracts.
