#!/usr/bin/env bash
# Deletes the Azure resource group provisioned by provision-azure.sh.
# Deleting the RG cascades to every resource inside (Cosmos, AI Search, OpenAI).
# **Hard-stops the Azure billing meter for these resources.**
#
# Usage:
#   ./scripts/credentials/teardown-azure.sh                          # uses default RG name
#   ./scripts/credentials/teardown-azure.sh --rg my-rg
#   ./scripts/credentials/teardown-azure.sh --yes                    # skip confirmation
set -euo pipefail

RG="${RG:-polarsharp-test-rg}"
SKIP_CONFIRM=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rg)     RG="$2"; shift 2 ;;
    --yes|-y) SKIP_CONFIRM=1; shift ;;
    -h|--help)
      grep '^#' "$0" | head -15 | sed 's/^#\s\?//'; exit 0 ;;
    *) echo "unknown arg: $1" >&2; exit 1 ;;
  esac
done

if ! command -v az >/dev/null 2>&1; then
  echo "az CLI not installed." >&2; exit 1
fi
if ! az account show >/dev/null 2>&1; then
  echo "az not signed in. Run: az login" >&2; exit 1
fi

if ! az group show --name "$RG" >/dev/null 2>&1; then
  printf "Resource group \033[36m%s\033[0m does not exist (or already deleted). Nothing to do.\n" "$RG"
  exit 0
fi

# Enumerate resources for the confirmation prompt so the user sees what's about to die
echo "Resources currently in $RG:"
az resource list --resource-group "$RG" --query '[].{name:name,type:type}' -o table || true
echo

if [[ $SKIP_CONFIRM -eq 0 ]]; then
  read -rp "Delete resource group '$RG' and ALL its resources? [type 'yes' to confirm]: " ANSWER
  if [[ "$ANSWER" != "yes" ]]; then
    echo "Aborted. Resource group left intact."
    exit 0
  fi
fi

printf "Deleting %s … (this is async; resources keep billing until Azure completes deletion)\n" "$RG"
az group delete --name "$RG" --yes --no-wait

cat <<EOF

\033[32mDeletion submitted.\033[0m Azure typically finishes within 5-10 minutes.

To verify completion:
  az group show --name $RG    # should return 'ResourceGroupNotFound' when done

Local .env still contains the (now-stale) credentials. Remove them with:
  for k in COSMOS_ACCOUNT_ENDPOINT COSMOS_ACCOUNT_KEY \\
           AZURE_SEARCH_SERVICE_NAME AZURE_SEARCH_ENDPOINT AZURE_SEARCH_ADMIN_KEY \\
           AZURE_OPENAI_ENDPOINT AZURE_OPENAI_API_KEY AZURE_OPENAI_DEPLOYMENT; do
    sed -i '' "/^\${k}=/d" .env
  done

(The sync-to-github-secrets.sh script does NOT delete GitHub secrets on teardown by design —
remove them manually with 'gh secret delete' if you want CI to skip those tests.)
EOF
