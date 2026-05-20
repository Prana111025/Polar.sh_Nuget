#!/bin/bash
# pre-merge-gate.sh — the contract every agent must pass before requesting merge.
#
# Per AGENT-COORDINATION.md section 5, the gate enforces:
#   1. Clean Release build (0 warnings, 0 errors)
#   2. Full unit test suite passes (filters out Category=Integration; agents run
#      integration tests separately + note results in the merge request)
#   3. Storefront lift-shift CI guard (when present + when relevant)
#   4. DocFX build (when DocFX has changes in the branch, to catch broken cross-references)
#   5. Working tree is clean (everything committed)
#
# Exits non-zero on any failure. Prints a clear pass/fail summary at the end.
#
# Usage:
#   ./scripts/pre-merge-gate.sh                       # run all checks
#   ./scripts/pre-merge-gate.sh --skip-docfx          # skip the DocFX build (rare)
#   ./scripts/pre-merge-gate.sh --skip-lift-shift     # skip the lift-shift guard (rare; only when agent's branch demonstrably doesn't touch storefront-core)

set -u  # error on undefined; we manage exit codes manually so set -e is intentionally OFF

SKIP_DOCFX=0
SKIP_LIFT_SHIFT=0
for arg in "$@"; do
    case "${arg}" in
        --skip-docfx) SKIP_DOCFX=1 ;;
        --skip-lift-shift) SKIP_LIFT_SHIFT=1 ;;
        *)
            echo "ERROR: unknown flag '${arg}'. Usage: $0 [--skip-docfx] [--skip-lift-shift]" >&2
            exit 64
            ;;
    esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${REPO_ROOT}"

# Track each step's outcome so the final summary is accurate even when a step is skipped.
STEP_BUILD="not run"
STEP_TESTS="not run"
STEP_LIFT_SHIFT="not run"
STEP_DOCFX="not run"
STEP_CLEAN_TREE="not run"

OVERALL_OK=1
mark_fail() { OVERALL_OK=0; }

print_step_header() {
    echo ""
    echo "════════════════════════════════════════════════════════════════════════════════"
    echo "  ${1}"
    echo "════════════════════════════════════════════════════════════════════════════════"
}

# ── 1. Release build ─────────────────────────────────────────────────────────────────────
print_step_header "1/5 — dotnet build (Release; 0 warnings, 0 errors)"
if dotnet build PolarSharp.slnx --configuration Release 2>&1 | tee /tmp/pmg-build.log | tail -20; then
    if grep -qE "Build succeeded\.$" /tmp/pmg-build.log; then
        # Also enforce warnings = 0 (CS1591 is already build-error; this catches any other warnings escalated)
        WARN_COUNT=$(grep -cE "^[[:space:]]+[0-9]+ Warning\(s\)$" /tmp/pmg-build.log | head -1)
        ACTUAL_WARNS=$(grep -E "^[[:space:]]+[0-9]+ Warning\(s\)$" /tmp/pmg-build.log | tail -1 | grep -oE "[0-9]+")
        if [[ "${ACTUAL_WARNS:-0}" == "0" ]]; then
            STEP_BUILD="PASS"
        else
            STEP_BUILD="FAIL (${ACTUAL_WARNS} warnings)"
            mark_fail
        fi
    else
        STEP_BUILD="FAIL (build did not succeed)"
        mark_fail
    fi
else
    STEP_BUILD="FAIL (dotnet build exit non-zero)"
    mark_fail
fi

# ── 2. Unit tests ────────────────────────────────────────────────────────────────────────
print_step_header "2/5 — dotnet test (unit tests; Category!=Integration)"
if [[ "${STEP_BUILD}" == "PASS" ]]; then
    if dotnet test PolarSharp.slnx --configuration Release --no-build --filter "Category!=Integration" --logger "console;verbosity=minimal" 2>&1 | tee /tmp/pmg-tests.log | tail -20; then
        FAILED_COUNT=$(grep -cE "Failed!\s+- Failed:" /tmp/pmg-tests.log || true)
        if [[ "${FAILED_COUNT}" == "0" ]]; then
            PASS_LINES=$(grep -cE "^Passed!\s+- Failed:\s+0," /tmp/pmg-tests.log || true)
            STEP_TESTS="PASS (${PASS_LINES} test projects, all green)"
        else
            STEP_TESTS="FAIL (${FAILED_COUNT} test projects had failures)"
            mark_fail
        fi
    else
        STEP_TESTS="FAIL (dotnet test exit non-zero)"
        mark_fail
    fi
else
    STEP_TESTS="SKIPPED (build failed; nothing to test)"
    mark_fail
fi

# ── 3. Lift-shift CI guard ──────────────────────────────────────────────────────────────
print_step_header "3/5 — lift-shift CI guard (storefronts; ensures *.Polar.*-free core)"
if [[ "${SKIP_LIFT_SHIFT}" == "1" ]]; then
    STEP_LIFT_SHIFT="SKIPPED (--skip-lift-shift)"
elif [[ -x ./scripts/verify-storefronts-no-polarsharp-deps.sh ]]; then
    if ./scripts/verify-storefronts-no-polarsharp-deps.sh; then
        STEP_LIFT_SHIFT="PASS"
    else
        STEP_LIFT_SHIFT="FAIL (lift-shift guard rejected the branch)"
        mark_fail
    fi
else
    STEP_LIFT_SHIFT="SKIPPED (guard script not present yet)"
fi

# Future: wallet-core lift-shift guard ships with Phase 22; add a parallel check here:
# if [[ -x ./scripts/verify-prepaidwallets-no-polarsharp-deps.sh ]]; then ...

# ── 4. DocFX build ──────────────────────────────────────────────────────────────────────
print_step_header "4/5 — docfx build (catches broken <see cref> and broken Markdown links)"
if [[ "${SKIP_DOCFX}" == "1" ]]; then
    STEP_DOCFX="SKIPPED (--skip-docfx)"
elif ! command -v docfx >/dev/null 2>&1; then
    STEP_DOCFX="SKIPPED (docfx CLI not installed on this host)"
elif [[ ! -f docfx.json ]]; then
    STEP_DOCFX="SKIPPED (no docfx.json at repo root)"
else
    if docfx build docfx.json --warningsAsErrors 2>&1 | tee /tmp/pmg-docfx.log | tail -15; then
        STEP_DOCFX="PASS"
    else
        STEP_DOCFX="FAIL (docfx build exit non-zero — check /tmp/pmg-docfx.log for broken refs)"
        mark_fail
    fi
fi

# ── 5. Working-tree cleanliness ────────────────────────────────────────────────────────
print_step_header "5/5 — git working tree must be clean (everything committed)"
DIRTY=$(git status --porcelain 2>&1 | head -20)
if [[ -z "${DIRTY}" ]]; then
    STEP_CLEAN_TREE="PASS"
else
    STEP_CLEAN_TREE="FAIL (uncommitted changes — commit or stash before requesting merge)"
    echo ""
    echo "Uncommitted changes:"
    echo "${DIRTY}" | head -20
    mark_fail
fi

# ── Summary ────────────────────────────────────────────────────────────────────────────
echo ""
echo "════════════════════════════════════════════════════════════════════════════════"
echo "  PRE-MERGE GATE SUMMARY"
echo "════════════════════════════════════════════════════════════════════════════════"
printf "  %-30s %s\n" "1. dotnet build (Release):"     "${STEP_BUILD}"
printf "  %-30s %s\n" "2. dotnet test (unit):"          "${STEP_TESTS}"
printf "  %-30s %s\n" "3. lift-shift CI guard:"         "${STEP_LIFT_SHIFT}"
printf "  %-30s %s\n" "4. docfx build:"                 "${STEP_DOCFX}"
printf "  %-30s %s\n" "5. clean working tree:"          "${STEP_CLEAN_TREE}"
echo "────────────────────────────────────────────────────────────────────────────────"
if [[ "${OVERALL_OK}" == "1" ]]; then
    echo "  RESULT: PASS — branch ready for merge request."
    echo "  Reminder: also run your phase's integration tests (Category=Integration) and"
    echo "  note the result in your merge-request body, per AGENT-COORDINATION.md §6."
    exit 0
else
    echo "  RESULT: FAIL — fix the failing steps above before requesting merge."
    exit 1
fi
