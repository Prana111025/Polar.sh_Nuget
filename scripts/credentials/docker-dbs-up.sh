#!/usr/bin/env bash
# Brings up Docker-hosted dev databases for PolarSharp development:
#   - Neo4j 5 community (graph DB for CustomerGraph scaffold)
#   - MeiliSearch (search backend, OSS alternative to Azure AI Search)
#   - PostgreSQL 17 alpine (Marten event-store dev workspace)
#
# Containers are named with the `polarsharp-dev-` prefix so teardown can target them.
# Connection details get written to .env. Idempotent — won't double-start existing containers.
#
# Note: the actual test suite uses Testcontainers per-test (not these long-lived containers).
# These containers are for *manual* dev work: poking at a graph in Neo4j Browser, exploring
# the Marten schema in psql, etc.
set -euo pipefail

ENV_FILE="${ENV_FILE:-$(dirname "$0")/../../.env}"

NEO4J_NAME="polarsharp-dev-neo4j"
NEO4J_PASSWORD="${NEO4J_PASSWORD:-devpassword}"
NEO4J_HTTP_PORT="${NEO4J_HTTP_PORT:-7474}"
NEO4J_BOLT_PORT="${NEO4J_BOLT_PORT:-7687}"

MEILI_NAME="polarsharp-dev-meilisearch"
MEILI_KEY="${MEILI_KEY:-devmasterkey}"
MEILI_PORT="${MEILI_PORT:-7700}"

PG_NAME="polarsharp-dev-marten-postgres"
PG_PASSWORD="${PG_PASSWORD:-devpassword}"
PG_PORT="${PG_PORT:-54320}"  # non-default so it doesn't collide with a host postgres
PG_DB="${PG_DB:-polar_marten_dev}"

# ── preflight ─────────────────────────────────────────────────────────────────
if ! command -v docker >/dev/null 2>&1; then
  echo "docker CLI not found." >&2; exit 1
fi
if ! docker info >/dev/null 2>&1; then
  echo "Docker daemon not running. Start Docker Desktop and re-try." >&2; exit 1
fi

upsert_env() {
  local key="$1" value="$2"
  local tmp
  tmp=$(mktemp)
  if [[ -f "$ENV_FILE" ]]; then grep -v "^${key}=" "$ENV_FILE" > "$tmp" || true; fi
  printf '%s=%s\n' "$key" "$value" >> "$tmp"
  mv "$tmp" "$ENV_FILE"
}

# Returns 0 if the named container exists (running or stopped); 1 otherwise.
container_exists() {
  docker ps -a --format '{{.Names}}' | grep -qx "$1"
}
container_running() {
  docker ps --format '{{.Names}}' | grep -qx "$1"
}

start_or_create() {
  local name="$1"; shift
  local image="$1"; shift
  if container_running "$name"; then
    printf "  \033[33m·\033[0m %s already running; reusing\n" "$name"
  elif container_exists "$name"; then
    docker start "$name" >/dev/null
    printf "  \033[32m✓\033[0m %s restarted\n" "$name"
  else
    docker run -d --name "$name" \
      --label purpose=polarsharp-dev \
      --restart unless-stopped \
      "$@" \
      "$image" >/dev/null
    printf "  \033[32m✓\033[0m %s created (image: %s)\n" "$name" "$image"
  fi
}

# ── 1. Neo4j ──────────────────────────────────────────────────────────────────
echo "1. Neo4j 5 community"
start_or_create "$NEO4J_NAME" "neo4j:5-community" \
  -p "$NEO4J_HTTP_PORT:7474" \
  -p "$NEO4J_BOLT_PORT:7687" \
  -e "NEO4J_AUTH=neo4j/$NEO4J_PASSWORD" \
  -v "polarsharp-neo4j-data:/data"
upsert_env "NEO4J_URI" "bolt://localhost:$NEO4J_BOLT_PORT"
upsert_env "NEO4J_USERNAME" "neo4j"
upsert_env "NEO4J_PASSWORD" "$NEO4J_PASSWORD"
printf "  Neo4j Browser: \033[36mhttp://localhost:%s\033[0m\n" "$NEO4J_HTTP_PORT"
echo

# ── 2. MeiliSearch ────────────────────────────────────────────────────────────
echo "2. MeiliSearch"
start_or_create "$MEILI_NAME" "getmeili/meilisearch:latest" \
  -p "$MEILI_PORT:7700" \
  -e "MEILI_MASTER_KEY=$MEILI_KEY" \
  -e "MEILI_ENV=development" \
  -v "polarsharp-meili-data:/meili_data"
upsert_env "MEILISEARCH_HOST" "http://localhost:$MEILI_PORT"
upsert_env "MEILISEARCH_MASTER_KEY" "$MEILI_KEY"
printf "  MeiliSearch: \033[36mhttp://localhost:%s\033[0m\n" "$MEILI_PORT"
echo

# ── 3. Postgres for Marten ────────────────────────────────────────────────────
echo "3. PostgreSQL 17 alpine (for Marten dev — Marten auto-creates its schema on first use)"
start_or_create "$PG_NAME" "postgres:17-alpine" \
  -p "$PG_PORT:5432" \
  -e "POSTGRES_PASSWORD=$PG_PASSWORD" \
  -e "POSTGRES_DB=$PG_DB" \
  -v "polarsharp-pg-data:/var/lib/postgresql/data"
MARTEN_CONN="Host=localhost;Port=$PG_PORT;Database=$PG_DB;Username=postgres;Password=$PG_PASSWORD"
upsert_env "MARTEN_CONNECTION_STRING" "$MARTEN_CONN"
printf "  Postgres: \033[36mlocalhost:%s\033[0m (db=%s)\n" "$PG_PORT" "$PG_DB"
echo

# ── done ──────────────────────────────────────────────────────────────────────
cat <<EOF
\033[32mAll three Docker dev DBs are up.\033[0m

Connect:
  Neo4j Browser            http://localhost:$NEO4J_HTTP_PORT       (neo4j / $NEO4J_PASSWORD)
  MeiliSearch              http://localhost:$MEILI_PORT            (X-Meili-API-Key: $MEILI_KEY)
  Postgres for Marten      psql 'postgres://postgres:$PG_PASSWORD@localhost:$PG_PORT/$PG_DB'

Tear down (containers stop + remove; data volumes persist for next run):
  ./scripts/credentials/docker-dbs-down.sh

Full wipe (also removes data volumes):
  ./scripts/credentials/docker-dbs-down.sh --wipe
EOF
