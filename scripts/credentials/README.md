# Credentials automation toolkit

Scripts to provision (and tear down) the 3rd-party services PolarSharp's full test suite needs, so you don't have to click through provider portals manually.

## What's automatable vs not

| Category | Providers | Automation |
|---|---|---|
| **Fully scriptable** | Azure (AI Search, OpenAI, Cosmos DB), AWS (OpenSearch), GCP (Vertex AI Search), Cloudflare (Images / R2 / Workers), Docker dev DBs (Neo4j, MeiliSearch, Postgres-for-Marten) | YES — provision + teardown via cloud CLIs (`az` / `aws` / `gcloud`) + Docker. Scripts in this directory. |
| **Browser-only signup** | Anthropic, OpenAI, xAI Grok, SendGrid, Twilio, TaxJar, EasyPost, Shippo | NO — sign-up requires a web flow + email/phone verification. Once you have an API key, `sync-to-github-secrets.sh` can push it to CI. See `MANUAL-STEPS.md`. |
| **Already done** | Polar.sh (`POLAR_SANDBOX_TOKEN`), GitHub (`GITHUB_TOKEN`), Telerik (`TELERIK_LICENSE_KEY`) | — |

## Usage — the happy path

```sh
# 1. Install missing CLIs (script checks + tells you what's missing)
./scripts/credentials/preflight.sh

# 2. Provision Azure resources (Search + OpenAI + Cosmos in one resource group)
./scripts/credentials/provision-azure.sh

# 3. Bring up Docker dev databases (Neo4j + MeiliSearch + Postgres-for-Marten)
./scripts/credentials/docker-dbs-up.sh

# 4. Push captured .env values to GitHub Actions secrets
./scripts/credentials/sync-to-github-secrets.sh

# … work / test cycle …

# 5. Tear down Docker DBs (frees machine resources)
./scripts/credentials/docker-dbs-down.sh

# 6. Tear down Azure resources (stops the billing meter)
./scripts/credentials/teardown-azure.sh
```

## What each script does

- **`preflight.sh`** — checks for `az`, `gh`, `docker`. Prints install instructions for whatever's missing. Idempotent.
- **`provision-azure.sh`** — creates a resource group `polarsharp-test-rg` (or whatever you pass via `--rg`) and inside it provisions: Cosmos DB (serverless), Azure AI Search (free tier), Azure OpenAI (S0 tier). Captures every credential to `.env`. Idempotent — re-running is safe; existing resources are reused.
- **`teardown-azure.sh`** — deletes the resource group, which deletes every Azure resource provisioned by `provision-azure.sh`. **Hard-stops billing.** Idempotent.
- **`docker-dbs-up.sh`** — `docker run -d` for `neo4j:5-community`, `getmeili/meilisearch:latest`, `postgres:17-alpine` (for Marten dev). Writes connection details to `.env`. Idempotent (won't double-start).
- **`docker-dbs-down.sh`** — `docker stop` + `docker rm` for the three containers. Idempotent.
- **`sync-to-github-secrets.sh`** — reads `.env`, pushes each var to GitHub Actions secrets via `gh secret set`. **Does not echo values into shell history or this AI assistant's context** (per memory note `feedback_secret_rotation_via_pbpaste.md`).

## Provider-specific scripts (extend the toolkit)

The Azure-only scripts cover the most common path. To extend to other clouds, follow the same pattern — see `provision-azure.sh` as the template:

- `provision-aws-opensearch.sh` (TODO) — `aws opensearch create-domain` + capture endpoint URL + IAM key pair
- `provision-gcp-gemini.sh` (TODO) — `gcloud services enable generativelanguage.googleapis.com` + key creation
- `provision-cloudflare.sh` (TODO) — `curl` against Cloudflare's REST API to create an API token

PRs welcome — keep teardown scripts paired with provisioning scripts so users never have orphaned cloud resources.

## Safety + idempotency contract

Every script in this directory:

- **Idempotent on re-run** — running twice is safe; existing resources are reused or skipped, not duplicated.
- **Tags every cloud resource** with `purpose=polarsharp-testing` so teardown can target only what we created.
- **Writes credentials to `.env`** in append-or-update fashion (`grep -v PATTERN .env > .env.tmp` then add the new line). Doesn't clobber unrelated env vars.
- **Never echoes secrets** into stdout. Uses temp files / process substitution + `pbpaste` patterns.
- **Returns non-zero on failure** — safe to chain with `&&` in larger pipelines.
- **Prints exactly what it did at the end** — resource names, regions, dollar-cost estimates.

## Browser-required providers — see MANUAL-STEPS.md

For Anthropic / OpenAI / xAI Grok / SendGrid / Twilio / TaxJar / EasyPost / Shippo — these all require browser signup (email verification, phone verification, sometimes credit card). Once you have an API key, paste it into `.env` and run `sync-to-github-secrets.sh` to fan-out to CI.

The full manual-steps checklist is in `MANUAL-STEPS.md` in this directory.

## Cost-awareness notes

Even with serverless / free tiers, cloud resources cost money. Defaults chosen for cheap-as-possible-but-functional:

- **Cosmos DB** — Serverless capacity mode (~$0.25 per million RUs); auto-pauses when idle
- **Azure AI Search** — Free tier (50 MB storage, 3 indexes, 10K docs); no upgrade needed for test traffic
- **Azure OpenAI** — Pay-per-token; ~$0.15 per million tokens on gpt-4o-mini
- **Docker dev DBs** — free (your machine's resources)

Estimated monthly cost when test suite runs nightly in CI: **~$2-5/month**, almost all of which is Cosmos burst RUs during integration test runs. Set the resource group to "stopped" between test cycles (or just run `teardown-azure.sh`) to drop to ~$0.

If you want to monitor: `az consumption usage list --resource-group polarsharp-test-rg` shows current spend.
