---
name: Count packages via csproj find, not src/ ls
description: Polar.sh_Nuget ships 31 NuGet packages — 30 from src/ plus PolarSharp.Templates from templates/. Never claim a package count from `ls src/` alone.
type: feedback
originSessionId: bb07392e-2cf6-4940-b228-806e1d187270
---
**Rule:** the authoritative package count for this repo is `find . -name "*.csproj" -not -path "*/bin/*" -not -path "*/obj/*" -not -path "*/tests/*" -not -path "*/testapp/*"`, not `ls src/`. The repo ships 31 packages total: 30 under `src/` + 1 (`PolarSharp.Templates`) under `templates/`.

**Why:** 2026-05-13 v1.3.H pre-commit audit. I asked the doc-audit agent to verify the README's "31 packages" claim. The agent ran `ls src/` (which I implicitly steered it toward by phrasing the prompt around `src/PolarSharp.*`), counted 30 directories, and reported "README claims 31 but only 30 exist." I asked the user via AskUserQuestion which they preferred — fix README to 30 or investigate which package was missing — and they (correctly, given the bad data) chose "Fix README to 30." I then changed README in 7 places, wrote a CHANGELOG entry stating "PolarSharp.Templates never scaffolded", and shipped that to v1.3.0. The mistake was caught only when verifying the publish artifacts on GitHub Packages — the CI publish log showed `PolarSharp.Templates.1.2.1.nupkg` being packed and pushed alongside the 30 src-tree packages. Reverted in commit `d84b944` after v1.3.0 had already shipped.

**How to apply:**
- Before claiming a package count for this repo, always run `find . -name "*.csproj" -not -path "*/bin/*" -not -path "*/obj/*" -not -path "*/tests/*" -not -path "*/testapp/*" | wc -l` from the repo root.
- When delegating "count the packages" or "audit the package inventory" to a subagent, explicitly point them at `find` over the whole tree, not `ls src/`. Phrase: "Use `find . -name '*.csproj'` excluding bin/obj/tests/testapp — packages live under both src/ AND templates/."
- Cross-check against the CI publish log (`.github/workflows/ci.yml` `Pack` and `Push` steps) when verifying release output — that's the ground truth for what actually shipped.
- Apply this caution to ANY claim about repo inventory: project counts, test project counts, doc article counts. The location convention is not "everything is under src/" — `templates/`, `testapp/`, `tests/`, `docs/`, and the repo root all carry meaningful artifacts.
