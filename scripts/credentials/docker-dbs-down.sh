#!/usr/bin/env bash
# Stops + removes the Docker dev DBs brought up by docker-dbs-up.sh.
# Data volumes persist across runs unless --wipe is passed.
#
# Usage:
#   ./scripts/credentials/docker-dbs-down.sh
#   ./scripts/credentials/docker-dbs-down.sh --wipe       # also removes data volumes
set -euo pipefail

WIPE=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --wipe) WIPE=1; shift ;;
    -h|--help) grep '^#' "$0" | head -10 | sed 's/^#\s\?//'; exit 0 ;;
    *) echo "unknown arg: $1" >&2; exit 1 ;;
  esac
done

CONTAINERS=(polarsharp-dev-neo4j polarsharp-dev-meilisearch polarsharp-dev-marten-postgres)
VOLUMES=(polarsharp-neo4j-data polarsharp-meili-data polarsharp-pg-data)

for c in "${CONTAINERS[@]}"; do
  if docker ps -a --format '{{.Names}}' | grep -qx "$c"; then
    docker stop "$c" >/dev/null 2>&1 || true
    docker rm "$c" >/dev/null
    printf "  \033[32m✓\033[0m removed container %s\n" "$c"
  else
    printf "  \033[33m·\033[0m container %s not present; nothing to do\n" "$c"
  fi
done

if [[ $WIPE -eq 1 ]]; then
  for v in "${VOLUMES[@]}"; do
    if docker volume ls --format '{{.Name}}' | grep -qx "$v"; then
      docker volume rm "$v" >/dev/null
      printf "  \033[32m✓\033[0m removed volume %s\n" "$v"
    fi
  done
  printf "\033[32mWipe complete\033[0m — next docker-dbs-up.sh starts from empty data.\n"
else
  printf "\nData volumes preserved; next docker-dbs-up.sh resumes from current state.\n"
  printf "To also remove data: \033[36m./scripts/credentials/docker-dbs-down.sh --wipe\033[0m\n"
fi
