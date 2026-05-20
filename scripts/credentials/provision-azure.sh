#!/usr/bin/env bash
# Provisions the Azure resources PolarSharp's test suite needs:
#   - Resource group (default name: polarsharp-test-rg, region: eastus2)
#   - Azure AI Search (free tier — 50 MB / 3 indexes / 10K docs)
#   - Azure OpenAI (S0 + gpt-4o-mini deployment)
#   - Cosmos DB account (Serverless, SQL API)
#
# Captures every credential into .env (append-or-update; never clobbers unrelated vars).
# Idempotent — re-running reuses existing resources.
#
# Usage:
#   ./scripts/credentials/provision-azure.sh                          # defaults
#   ./scripts/credentials/provision-azure.sh --rg my-rg --region eastus
#   ./scripts/credentials/provision-azure.sh --dry-run                # print plan, no changes
#
# Teardown: ./scripts/credentials/teardown-azure.sh
set -euo pipefail

# ── defaults ──────────────────────────────────────────────────────────────────
RG="${RG:-polarsharp-test-rg}"
REGION="${REGION:-eastus2}"
DRY_RUN=0
TAG_KEY="purpose"
TAG_VALUE="polarsharp-testing"

# Resource names (must be globally unique for some services — use a short hash)
SUFFIX="$(echo -n "$RG" | shasum | cut -c1-6)"
COSMOS_NAME="polar-cosmos-$SUFFIX"
SEARCH_NAME="polar-search-$SUFFIX"
OPENAI_NAME="polar-openai-$SUFFIX"
OPENAI_DEPLOYMENT="gpt-4o-mini"

ENV_FILE="${ENV_FILE:-$(dirname "$0")/../../.env}"

# ── arg parsing ───────────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
  case "$1" in
    --rg)       RG="$2"; shift 2 ;;
    --region)   REGION="$2"; shift 2 ;;
    --dry-run)  DRY_RUN=1; shift ;;
    -h|--help)
      grep '^#' "$0" | head -25 | sed 's/^#\s\?//'
      exit 0 ;;
    *)
      echo "unknown arg: $1" >&2; exit 1 ;;
  esac
done

# ── preflight ─────────────────────────────────────────────────────────────────
if ! command -v az >/dev/null 2>&1; then
  echo "az CLI not installed. Run: brew install azure-cli" >&2; exit 1
fi
if ! az account show >/dev/null 2>&1; then
  echo "az not signed in. Run: az login" >&2; exit 1
fi
SUB_NAME=$(az account show --query name -o tsv)
SUB_ID=$(az account show --query id -o tsv)
printf "Subscription: \033[36m%s\033[0m (%s)\n" "$SUB_NAME" "$SUB_ID"
printf "Resource group: \033[36m%s\033[0m in region \033[36m%s\033[0m\n" "$RG" "$REGION"
echo

run() {
  if [[ $DRY_RUN -eq 1 ]]; then
    printf "  \033[2m(dry-run)\033[0m %s\n" "$*"
  else
    "$@"
  fi
}

upsert_env() {
  local key="$1"
  local value="$2"
  # Append-or-update without clobbering other vars or echoing the value to stdout.
  # Uses a temp file so we never write a partial .env if the script is interrupted.
  local tmp
  tmp=$(mktemp)
  if [[ -f "$ENV_FILE" ]]; then
    grep -v "^${key}=" "$ENV_FILE" > "$tmp" || true
  fi
  printf '%s=%s\n' "$key" "$value" >> "$tmp"
  mv "$tmp" "$ENV_FILE"
  printf "  \033[32m✓\033[0m updated .env: %s=…\n" "$key"
}

# ── 1. Resource group ─────────────────────────────────────────────────────────
echo "1. Resource group"
if az group show --name "$RG" >/dev/null 2>&1; then
  printf "  \033[33m·\033[0m %s exists; reusing\n" "$RG"
else
  run az group create --name "$RG" --location "$REGION" --tags "$TAG_KEY=$TAG_VALUE" >/dev/null
  printf "  \033[32m✓\033[0m created\n"
fi
echo

# ── 2. Cosmos DB ──────────────────────────────────────────────────────────────
echo "2. Cosmos DB (Serverless, SQL API) — $COSMOS_NAME"
if az cosmosdb show --name "$COSMOS_NAME" --resource-group "$RG" >/dev/null 2>&1; then
  printf "  \033[33m·\033[0m exists; reusing\n"
else
  run az cosmosdb create \
    --name "$COSMOS_NAME" \
    --resource-group "$RG" \
    --capabilities EnableServerless \
    --default-consistency-level Session \
    --locations regionName="$REGION" failoverPriority=0 isZoneRedundant=False \
    --tags "$TAG_KEY=$TAG_VALUE" \
    >/dev/null
  printf "  \033[32m✓\033[0m created (this can take 5-10 minutes — Cosmos is slow to provision)\n"
fi
if [[ $DRY_RUN -eq 0 ]]; then
  COSMOS_ENDPOINT="https://${COSMOS_NAME}.documents.azure.com:443/"
  COSMOS_KEY=$(az cosmosdb keys list --name "$COSMOS_NAME" --resource-group "$RG" --query primaryMasterKey -o tsv)
  upsert_env "COSMOS_ACCOUNT_ENDPOINT" "$COSMOS_ENDPOINT"
  upsert_env "COSMOS_ACCOUNT_KEY" "$COSMOS_KEY"
fi
echo

# ── 3. Azure AI Search ────────────────────────────────────────────────────────
echo "3. Azure AI Search (free tier) — $SEARCH_NAME"
if az search service show --name "$SEARCH_NAME" --resource-group "$RG" >/dev/null 2>&1; then
  printf "  \033[33m·\033[0m exists; reusing\n"
else
  run az search service create \
    --name "$SEARCH_NAME" \
    --resource-group "$RG" \
    --location "$REGION" \
    --sku free \
    --tags "$TAG_KEY=$TAG_VALUE" \
    >/dev/null
  printf "  \033[32m✓\033[0m created\n"
fi
if [[ $DRY_RUN -eq 0 ]]; then
  SEARCH_ENDPOINT="https://${SEARCH_NAME}.search.windows.net"
  SEARCH_KEY=$(az search admin-key show --service-name "$SEARCH_NAME" --resource-group "$RG" --query primaryKey -o tsv)
  upsert_env "AZURE_SEARCH_SERVICE_NAME" "$SEARCH_NAME"
  upsert_env "AZURE_SEARCH_ENDPOINT" "$SEARCH_ENDPOINT"
  upsert_env "AZURE_SEARCH_ADMIN_KEY" "$SEARCH_KEY"
fi
echo

# ── 4. Azure OpenAI ───────────────────────────────────────────────────────────
echo "4. Azure OpenAI (S0) — $OPENAI_NAME"
if az cognitiveservices account show --name "$OPENAI_NAME" --resource-group "$RG" >/dev/null 2>&1; then
  printf "  \033[33m·\033[0m exists; reusing\n"
else
  run az cognitiveservices account create \
    --name "$OPENAI_NAME" \
    --resource-group "$RG" \
    --location "$REGION" \
    --kind OpenAI \
    --sku S0 \
    --yes \
    --tags "$TAG_KEY=$TAG_VALUE" \
    >/dev/null
  printf "  \033[32m✓\033[0m created\n"
fi

if [[ $DRY_RUN -eq 0 ]]; then
  # Deploy gpt-4o-mini if not already deployed (cheapest sensible chat model)
  if az cognitiveservices account deployment show \
      --name "$OPENAI_NAME" --resource-group "$RG" \
      --deployment-name "$OPENAI_DEPLOYMENT" >/dev/null 2>&1; then
    printf "  \033[33m·\033[0m deployment %s exists; reusing\n" "$OPENAI_DEPLOYMENT"
  else
    run az cognitiveservices account deployment create \
      --name "$OPENAI_NAME" --resource-group "$RG" \
      --deployment-name "$OPENAI_DEPLOYMENT" \
      --model-name "$OPENAI_DEPLOYMENT" \
      --model-version "2024-07-18" \
      --model-format OpenAI \
      --sku-name "Standard" --sku-capacity 10 \
      >/dev/null
    printf "  \033[32m✓\033[0m deployed model %s\n" "$OPENAI_DEPLOYMENT"
  fi

  OPENAI_ENDPOINT=$(az cognitiveservices account show --name "$OPENAI_NAME" --resource-group "$RG" --query properties.endpoint -o tsv)
  OPENAI_KEY=$(az cognitiveservices account keys list --name "$OPENAI_NAME" --resource-group "$RG" --query key1 -o tsv)
  upsert_env "AZURE_OPENAI_ENDPOINT" "$OPENAI_ENDPOINT"
  upsert_env "AZURE_OPENAI_API_KEY" "$OPENAI_KEY"
  upsert_env "AZURE_OPENAI_DEPLOYMENT" "$OPENAI_DEPLOYMENT"
fi
echo

# ── done ──────────────────────────────────────────────────────────────────────
if [[ $DRY_RUN -eq 1 ]]; then
  echo "Dry run complete. Re-run without --dry-run to actually provision."
else
  cat <<EOF
\033[32mProvisioning complete.\033[0m

Resources in $RG:
  • cosmosdb         $COSMOS_NAME
  • search           $SEARCH_NAME (free tier)
  • openai           $OPENAI_NAME (deployment: $OPENAI_DEPLOYMENT)

Credentials written to .env. To push them to GitHub Actions secrets:
  ./scripts/credentials/sync-to-github-secrets.sh

To tear down everything (stops the billing meter):
  ./scripts/credentials/teardown-azure.sh --rg $RG
EOF
fi
