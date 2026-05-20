#!/usr/bin/env bash
# Checks for the CLIs the credentials-automation toolkit needs.
# Prints install instructions for anything missing. Exits 0 if everything's present,
# non-zero if at least one CLI is missing.
set -euo pipefail

MISSING=()

check() {
  local cmd="$1"
  local install_hint="$2"
  if command -v "$cmd" >/dev/null 2>&1; then
    printf "  \033[32m✓\033[0m %s (\033[2m%s\033[0m)\n" "$cmd" "$(command -v "$cmd")"
  else
    printf "  \033[31m✗\033[0m %s — MISSING\n     install: \033[36m%s\033[0m\n" "$cmd" "$install_hint"
    MISSING+=("$cmd")
  fi
}

echo "PolarSharp credentials toolkit — preflight check"
echo "================================================"
echo
echo "Required for cloud provisioning:"
check az      "brew install azure-cli"
check gh      "brew install gh"
check docker  "https://docs.docker.com/desktop/install/mac-install/"
echo
echo "Optional (extend the toolkit for these clouds):"
check aws     "brew install awscli"
check gcloud  "brew install --cask google-cloud-sdk"
echo
echo "Auth status:"
if command -v az >/dev/null 2>&1; then
  if az account show >/dev/null 2>&1; then
    SUB=$(az account show --query name -o tsv 2>/dev/null || echo "unknown")
    printf "  \033[32m✓\033[0m az: signed in to subscription \033[36m%s\033[0m\n" "$SUB"
  else
    printf "  \033[33m!\033[0m az: not signed in — run \033[36maz login\033[0m\n"
  fi
fi
if command -v gh >/dev/null 2>&1; then
  if gh auth status >/dev/null 2>&1; then
    printf "  \033[32m✓\033[0m gh: signed in\n"
  else
    printf "  \033[33m!\033[0m gh: not signed in — run \033[36mgh auth login\033[0m\n"
  fi
fi
if command -v docker >/dev/null 2>&1; then
  if docker info >/dev/null 2>&1; then
    printf "  \033[32m✓\033[0m docker: daemon running\n"
  else
    printf "  \033[33m!\033[0m docker: daemon not running — start Docker Desktop\n"
  fi
fi

echo
if [ ${#MISSING[@]} -eq 0 ]; then
  printf "\033[32mAll required CLIs present.\033[0m Run \033[36m./scripts/credentials/provision-azure.sh\033[0m next.\n"
  exit 0
else
  printf "\033[31m%d required CLI(s) missing.\033[0m Install per the hints above, then re-run preflight.\n" "${#MISSING[@]}"
  exit 1
fi
