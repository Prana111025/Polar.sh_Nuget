#!/usr/bin/env bash
# Pushes selected .env values to GitHub Actions repository secrets via `gh secret set`.
# Designed so secret VALUES never enter stdout, shell history, or this AI assistant's
# context (per memory note feedback_secret_rotation_via_pbpaste.md). The script reads
# values directly from .env via `gh secret set --body "$value"` where $value is bound
# but never printed.
#
# Usage:
#   ./scripts/credentials/sync-to-github-secrets.sh                  # syncs all known keys
#   ./scripts/credentials/sync-to-github-secrets.sh --repo other/repo
#   ./scripts/credentials/sync-to-github-secrets.sh --dry-run        # lists what'd sync
#   ./scripts/credentials/sync-to-github-secrets.sh --only AZURE_*   # glob filter
set -euo pipefail

REPO="${REPO:-MollsAndHersh/Polar.sh_Nuget}"
ENV_FILE="${ENV_FILE:-$(dirname "$0")/../../.env}"
DRY_RUN=0
ONLY_PATTERN="*"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo)     REPO="$2"; shift 2 ;;
    --dry-run)  DRY_RUN=1; shift ;;
    --only)     ONLY_PATTERN="$2"; shift 2 ;;
    -h|--help)  grep '^#' "$0" | head -12 | sed 's/^#\s\?//'; exit 0 ;;
    *) echo "unknown arg: $1" >&2; exit 1 ;;
  esac
done

# Keys we know about — extend this list as new providers come online.
KNOWN_KEYS=(
  # ── Polar.sh (already set in CI; included for completeness) ────────────
  POLAR_SANDBOX_TOKEN
  # ── AI translation providers (TASK-V14-007) ────────────────────────────
  ANTHROPIC_API_KEY OPENAI_API_KEY
  AZURE_OPENAI_API_KEY AZURE_OPENAI_ENDPOINT AZURE_OPENAI_DEPLOYMENT
  GEMINI_API_KEY GROK_API_KEY
  # ── Azure resources (provision-azure.sh) ───────────────────────────────
  COSMOS_ACCOUNT_ENDPOINT COSMOS_ACCOUNT_KEY
  AZURE_SEARCH_SERVICE_NAME AZURE_SEARCH_ENDPOINT AZURE_SEARCH_ADMIN_KEY
  # ── Notification providers ─────────────────────────────────────────────
  SENDGRID_API_KEY TWILIO_ACCOUNT_SID TWILIO_AUTH_TOKEN TWILIO_FROM_PHONE_NUMBER
  # ── Shipping/Tax (Phase 14.x) ──────────────────────────────────────────
  EASYPOST_API_KEY SHIPPO_API_KEY TAXJAR_API_KEY
  # ── Telerik license ────────────────────────────────────────────────────
  TELERIK_LICENSE_KEY
)

if ! command -v gh >/dev/null 2>&1; then echo "gh CLI not installed." >&2; exit 1; fi
if ! gh auth status >/dev/null 2>&1; then echo "gh not signed in. Run: gh auth login" >&2; exit 1; fi
if [[ ! -f "$ENV_FILE" ]]; then echo ".env not found at $ENV_FILE" >&2; exit 1; fi

set_secret() {
  local key="$1"
  # Extract value WITHOUT echoing it. Use printf-via-variable, then pipe.
  local value
  value=$(grep "^${key}=" "$ENV_FILE" | head -1 | cut -d= -f2- || true)
  if [[ -z "$value" ]]; then
    printf "  \033[33m·\033[0m %s not in .env; skipping\n" "$key"
    return 0
  fi
  if [[ $DRY_RUN -eq 1 ]]; then
    printf "  \033[2m(dry-run)\033[0m would set %s on %s\n" "$key" "$REPO"
    return 0
  fi
  # gh secret set --body reads the value from arg; it goes through the gh client
  # to GitHub's encrypted-secret endpoint. The value never goes to stdout.
  printf '%s' "$value" | gh secret set "$key" --repo "$REPO" >/dev/null
  printf "  \033[32m✓\033[0m %s\n" "$key"
}

printf "Syncing .env → GitHub Actions secrets on \033[36m%s\033[0m\n" "$REPO"
[[ "$ONLY_PATTERN" != "*" ]] && printf "Filter: \033[36m%s\033[0m\n" "$ONLY_PATTERN"
echo

for key in "${KNOWN_KEYS[@]}"; do
  # shellcheck disable=SC2254
  case "$key" in
    $ONLY_PATTERN) set_secret "$key" ;;
    *) ;;
  esac
done

echo
if [[ $DRY_RUN -eq 1 ]]; then
  echo "Dry run complete. Re-run without --dry-run to actually push."
else
  printf "Done. Verify with: \033[36mgh secret list --repo %s\033[0m\n" "$REPO"
fi
