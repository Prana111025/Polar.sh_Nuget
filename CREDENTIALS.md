# CREDENTIALS.md

Third-party service credentials needed to unlock the full PolarSharp test suite (and for production deployments). Each section is checklist-style so you can track acquisition progress.

**How this list was produced:** 2026-05-20 audit of every package under `src/` + every test under `tests/`. Re-run the audit (delegate to an Explore subagent with the prompt template at the bottom of this file) whenever a new integration package lands.

**Status legend:**
- ✅ Already established (locally set; in GitHub Actions secrets)
- 🔴 NEEDED NOW — closes a Tier A live-test gap
- 🟡 NEEDED at Phase X.x — package is currently a scaffold; acquire when implementation lands
- 🟢 Optional — only if you pursue the integration

---

## Already established (no action)

- [x] **Polar.sh sandbox** — `POLAR_SANDBOX_TOKEN` ✅ ([sandbox-api.polar.sh](https://sandbox-api.polar.sh)) — verified working in CI Integration job
- [x] **GitHub Packages** — `GITHUB_TOKEN` ✅ — used by CI publish job
- [x] **Telerik UI for Blazor** — `TELERIK_LICENSE_KEY` ✅ — per PLAN.md (used in `PolarSaasDemo` flagship; license file gitignored)

---

## 🔴 Section A — Acquire NOW (closes Tier A live-test gaps; first 5 close TASK-V14-007)

### A.1 Anthropic Claude — AI catalog translation

- [ ] Sign up at [console.anthropic.com](https://console.anthropic.com)
- [ ] Generate an API key
- [ ] Add to local `.env`: `ANTHROPIC_API_KEY=sk-ant-…`
- [ ] Add to GitHub Actions secrets: `ANTHROPIC_API_KEY`
- **Used by:** `src/PolarSharp.EcommerceStoreManagement.Translation.Anthropic/AnthropicCatalogTranslator.cs:20–35`
- **Test gated on it:** `tests/PolarSharp.EcommerceStoreManagement.Translation.Tests/LiveProviderIntegrationTests.cs::Anthropic_translator_round_trips_against_live_messages_api`
- **Endpoint hit:** `https://api.anthropic.com/v1/messages`

### A.2 OpenAI — AI catalog translation (rotate existing — currently 401)

- [ ] Sign in at [platform.openai.com](https://platform.openai.com)
- [ ] Rotate the existing key (current local `OPENAI_API_KEY` is rejected with 401)
- [ ] Update local `.env`: `OPENAI_API_KEY=sk-…`
- [ ] Update GitHub Actions secret: `OPENAI_API_KEY`
- **Used by:** `src/PolarSharp.EcommerceStoreManagement.Translation.OpenAI/OpenAiCatalogTranslator.cs`
- **Test gated on it:** `tests/PolarSharp.EcommerceStoreManagement.Translation.Tests/LiveProviderIntegrationTests.cs::OpenAI_translator_round_trips_against_live_chat_completions`
- **Endpoint hit:** `https://api.openai.com/v1/chat/completions`

### A.3 Azure OpenAI — AI catalog translation

- [ ] Create resource at [Azure Portal](https://portal.azure.com) → AI services → Azure OpenAI
- [ ] Deploy a chat model (e.g. `gpt-4o-mini`); name the deployment
- [ ] Capture endpoint URL + API key
- [ ] Add to local `.env`:
  - `AZURE_OPENAI_API_KEY=…`
  - `AZURE_OPENAI_ENDPOINT=https://{your-resource}.openai.azure.com`
  - `AZURE_OPENAI_DEPLOYMENT={your-deployment-name}`
- [ ] Add all three to GitHub Actions secrets
- **Used by:** `src/PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI/AzureOpenAiCatalogTranslator.cs`
- **Test gated on it:** `tests/.../LiveProviderIntegrationTests.cs::AzureOpenAI_translator_round_trips_against_live_deployment`
- **Endpoint format:** `https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions?api-version=2024-02-15-preview`

### A.4 Google Gemini — AI catalog translation

- [ ] Get an API key at [aistudio.google.com](https://aistudio.google.com) → "Get API key"
- [ ] Add to local `.env`: `GEMINI_API_KEY=…`
- [ ] Add to GitHub Actions secrets: `GEMINI_API_KEY`
- **Used by:** `src/PolarSharp.EcommerceStoreManagement.Translation.Gemini/GeminiCatalogTranslator.cs`
- **Test gated on it:** `tests/.../LiveProviderIntegrationTests.cs::Gemini_translator_round_trips_against_live_generative_language_api`
- **Endpoint hit:** `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent`

### A.5 xAI Grok — AI catalog translation

- [ ] Sign up at [console.x.ai](https://console.x.ai)
- [ ] Generate an API key (`xai-…` prefix)
- [ ] Add to local `.env`: `GROK_API_KEY=xai-…`
- [ ] Add to GitHub Actions secrets: `GROK_API_KEY`
- **Used by:** `src/PolarSharp.EcommerceStoreManagement.Translation.Grok/GrokCatalogTranslator.cs`
- **Test gated on it:** `tests/.../LiveProviderIntegrationTests.cs::Grok_translator_round_trips_against_live_xai_api`
- **Endpoint hit:** `https://api.x.ai/v1/chat/completions` (OpenAI-compatible shape)

### A.6 SendGrid — Tenant lifecycle email notifications

- [ ] Sign up at [app.sendgrid.com](https://app.sendgrid.com) (free dev tier: 100 emails/day)
- [ ] Settings → API Keys → Create API Key (with "Mail Send" permission)
- [ ] Verify a sender identity / domain
- [ ] Add to local `.env`: `SENDGRID_API_KEY=SG.…`
- [ ] Add to GitHub Actions secrets: `SENDGRID_API_KEY`
- **Used by:** `src/PolarSharp.MultiTenant.Notifications/Channels/SendGridEmailChannel.cs:62–70`
- **Endpoint hit:** `https://api.sendgrid.com/v3/mail/send`
- **Note:** env var name is configurable via `TenantNotificationOptions.Email.SendGrid.ApiKeyEnvVar`

### A.8 Azure Cosmos DB — multi-tenant + Identity + Catalog + Reporting Cosmos providers

- [ ] Create an Azure Cosmos DB account at [Azure Portal](https://portal.azure.com) → Azure Cosmos DB
  - **API:** Core (SQL)
  - **Capacity mode:** Serverless (cheapest for dev/test; pay-per-RU; no provisioned throughput cost)
  - **Region:** closest to your machine to minimize latency
- [ ] Capture the URI + Primary Key from the "Keys" blade
- [ ] Add to local `.env`:
  - `COSMOS_ACCOUNT_ENDPOINT=https://{your-account}.documents.azure.com:443/`
  - `COSMOS_ACCOUNT_KEY=…`
- [ ] Add both to GitHub Actions secrets
- **Used by:** Currently the 8 Cosmos integration tests in `tests/PolarSharp.MultiTenant.EntityFrameworkCore.Tests/Integration/CosmosDbSingleTenantUpgradeMigratorIntegrationTests.cs` are `[SkippableFact]` because the Cosmos Linux emulator can't reliably boot on macOS / many CI runners. Pointing them at a real serverless account unblocks them.
- **Why paid not emulator:** Per user decision 2026-05-20 — the Linux emulator is too unreliable for routine use; serverless Cosmos is cheap for the test workload (~$1-5/month for the test traffic volume; auto-pauses when idle).
- **Will also be used by** (once their Phase X.x lands and IsPackable flips back):
  - `src/PolarSharp.MultiTenant.Identity.CosmosDb/` (REAL today)
  - `src/PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.CosmosDb/` (REAL today)
  - `src/PolarSharp.Reporting.EntityFrameworkCore.CosmosDb/` (REAL today)
  - `src/PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.CosmosDb/` (scaffold; Phase 21.x)
- **Tear-down:** when not actively testing, set the account to "Free Tier" mode or delete the database (`Microsoft.Azure.Cosmos` SDK supports `Database.DeleteAsync`). Serverless billing pauses automatically when no RUs are consumed.

### A.7 Twilio — Tenant lifecycle SMS notifications

- [ ] Sign up at [twilio.com](https://www.twilio.com) (free trial credit available)
- [ ] Capture Account SID + Auth Token from console dashboard
- [ ] Purchase or claim a Twilio phone number for sending
- [ ] Add to local `.env`:
  - `TWILIO_ACCOUNT_SID=AC…`
  - `TWILIO_AUTH_TOKEN=…`
  - `TWILIO_FROM_PHONE_NUMBER=+1…`
- [ ] Add all three to GitHub Actions secrets
- **Used by:** `src/PolarSharp.MultiTenant.Notifications/Channels/TwilioSmsChannel.cs:48–60`
- **Endpoint hit:** `https://api.twilio.com/2010-04-01/Accounts/{AccountSid}/Messages.json`
- **Note:** env var names configurable via `TenantNotificationOptions.Sms.Twilio.AccountSidEnvVar` / `.AuthTokenEnvVar`

---

## 🟡 Section B — Acquire when their implementation phase lands

These packages are currently `IsPackable=false` scaffolds. The credentials aren't useful until the implementation ships; acquire on a per-phase basis.

### B.1 EasyPost — Shipping label & rate quotes (Phase 14.x)

- [ ] Sign up at [easypost.com](https://www.easypost.com) (test mode is free)
- [ ] Capture API key (test + production are separate)
- [ ] Add to local `.env`: `EASYPOST_API_KEY=EZAK…` (or `EZTK…` for test)
- **Will be used by:** `src/PolarSharp.EcommerceStorefronts.Shipping.EasyPost/` (scaffold today)
- **Tracking:** TASK-V14-???.shipping (Phase 14.x implementation)

### B.2 Shippo — Shipping label & rate quotes (Phase 14.x; alternative to EasyPost)

- [ ] Sign up at [goshippo.com](https://goshippo.com) (test mode free)
- [ ] Capture API key
- [ ] Add to local `.env`: `SHIPPO_API_KEY=shippo_test_…`
- **Will be used by:** `src/PolarSharp.EcommerceStorefronts.Shipping.Shippo/` (scaffold today)

### B.3 TaxJar — Sales tax / VAT / GST computation (Phase 14.x — only if pursued)

- [ ] Sign up at [taxjar.com](https://www.taxjar.com) (free sandbox)
- [ ] Capture API key (sandbox vs production keys differ)
- [ ] Add to local `.env`: `TAXJAR_API_KEY=…`
- **Will be used by:** `src/PolarSharp.EcommerceStorefronts.Tax.TaxJar/` (scaffold today)
- **Note:** the WTR framework's `BasicEstimatedTaxCalculator` is the default; TaxJar is an upgrade path. Only pursue if real-world tenant demand materializes.

### B.4 Catalog search (Phase 14.x) — pick a provider

**Recommended primary: Azure AI Search** — "big name" option per 2026-05-20 user direction. Pairs cleanly with the existing `Translation.AzureOpenAI` provider (same Azure account works for both); strong .NET ecosystem (`Azure.Search.Documents` SDK); built-in vector search + semantic ranker + faceted filtering; free tier suitable for dev. Implementation would land as a new package `PolarSharp.EcommerceStorefronts.Search.AzureAiSearch`.

- [ ] [Azure Portal](https://portal.azure.com) → Create Azure AI Search service
- [ ] Capture: service name + admin API key + (optionally) query API key
- [ ] Add to local `.env`:
  - `AZURE_SEARCH_SERVICE_NAME={your-service-name}`
  - `AZURE_SEARCH_ADMIN_KEY=…`
  - `AZURE_SEARCH_ENDPOINT=https://{your-service-name}.search.windows.net`
- **Tracking:** add a new package `PolarSharp.EcommerceStorefronts.Search.AzureAiSearch` when this lands (mirror the translation-provider per-vendor pattern)

**Alternative: AWS OpenSearch Service** — Elasticsearch-compatible managed service; mature ecosystem; serverless variant available. New package `PolarSharp.EcommerceStorefronts.Search.AwsOpenSearch` when pursued.

- [ ] AWS Console → OpenSearch Service → create serverless or domain instance
- [ ] Add to local `.env`:
  - `AWS_OPENSEARCH_ENDPOINT=…`
  - `AWS_OPENSEARCH_ACCESS_KEY_ID=…`
  - `AWS_OPENSEARCH_SECRET_ACCESS_KEY=…`

**Self-hosted fallback: MeiliSearch** — keeps the existing scaffold package useful for hosts who prefer OSS / self-host; runs in Docker (`docker run -d -p 7700:7700 -e MEILI_MASTER_KEY=devkey getmeili/meilisearch:latest`). Requires no external account.

- [ ] `MEILISEARCH_HOST=http://localhost:7700`
- [ ] `MEILISEARCH_MASTER_KEY=devkey`
- **Used by:** `src/PolarSharp.EcommerceStorefronts.Search.MeiliSearch/` (scaffold today)

**Skip:** Google Cloud Search is deprecated; GCP Vertex AI Search is newer but has a smaller .NET SDK community — pursue only if GCP is a strategic requirement.

### B.5 Neo4j — Customer relationship graph (Phase 17.x)

**Primary: Docker via Testcontainers** (per 2026-05-20 user direction — "Neo4J needs to be testable via a Docker Image as well"). The official `neo4j:5-community` image works with the generic `Testcontainers` builder (no `Testcontainers.Neo4j` NuGet package needed; map ports 7474 + 7687 + set `NEO4J_AUTH` env var). When Phase 17.x ships, mirror the test pattern from `MartenWalletEventStoreIntegrationTests.cs` — per-class `IAsyncLifetime` container lifecycle, image pin to `neo4j:5-community`, full test suite spins up + tears down per cycle.

**Dev (manual run, no Testcontainers):**
- [ ] `docker run -d -p 7474:7474 -p 7687:7687 -e NEO4J_AUTH=neo4j/devpassword neo4j:5-community`
- [ ] Add to local `.env`:
  - `NEO4J_URI=bolt://localhost:7687`
  - `NEO4J_USERNAME=neo4j`
  - `NEO4J_PASSWORD=devpassword`

**Production / cloud (optional):** Neo4j Aura at [neo4j.com/cloud/aura](https://neo4j.com/cloud/aura) — free tier available; capture URI + auto-generated password.

- **Will be used by:** `src/PolarSharp.CustomerGraph.Neo4j/` (scaffold today)
- **Note for production:** Neo4j Enterprise supports per-tenant DATABASE isolation; Community uses label-based isolation only. PolarSharp's 5-layer tenant isolation pattern needs Enterprise for `tenant_id`-as-database mode.
- **Tracking:** Phase 17.x implementation must include Testcontainer-backed integration tests using `neo4j:5-community` image. Add to TASKS.md as TASK-V14-???.neo4j when scheduled.

### B.6 PostgreSQL for Marten event store (Phase 15.x)

- **No external account needed** — Marten is self-hosted Postgres
- Tests use Testcontainers (auto-managed; no creds needed)
- Production requires a Postgres 12+ instance with `pgcrypto` extension
- **Will be used by:** `src/PolarSharp.AuditLog.Marten/`, `Reporting.Marten/`, `PrepaidWallets.EventStore.Marten/`, `Onboarding.Wizard.Marten/` (all scaffolds today)
- **Tracking:** TASK-V14-008 / Phase 15.x

---

## 🟢 Section C — Optional integrations

Only acquire if you pursue the integration.

### C.1 Cloudflare — Image optimization + R2 storage + Workers CDN

- [ ] Sign up at [dash.cloudflare.com](https://dash.cloudflare.com) (free tier covers most dev use)
- [ ] My Profile → API Tokens → create token with permissions:
  - `Cloudflare Images:Edit` (for image optimization)
  - `Workers Scripts:Edit` (for WebComponents CDN distribution per Phase 3)
  - `Workers R2 Storage:Edit` (for R2 buckets)
- [ ] Capture Account ID from dashboard sidebar
- [ ] Add to local `.env`:
  - `CLOUDFLARE_ACCOUNT_ID=…`
  - `CLOUDFLARE_API_TOKEN=…`
  - `CLOUDFLARE_ZONE_ID=…` (per domain)
- **Will be used by:** `src/PolarSharp.EcommerceStorefronts.SEO.Images.Cloudflare/` (scaffold) + planned `MediaAndFileStorage` package per Phase 3 design

### C.2 Imgix — Image optimization (alternative to Cloudflare Images)

- [ ] Sign up at [imgix.com](https://www.imgix.com)
- [ ] Create a Source (S3 / Cloudfront / web folder backing)
- [ ] Capture Source ID + API key
- [ ] Add to local `.env`:
  - `IMGIX_SOURCE_ID=…`
  - `IMGIX_API_KEY=…`
- **Will be used by:** `src/PolarSharp.EcommerceStorefronts.SEO.Images.Imgix/` (scaffold)

### C.3 Litestream replication target — pick ONE storage backend

For SQLite per-tenant `.db` file replication. The `LitestreamConfigGenerator` supports 4 backends:

**Option C.3.a — AWS S3:**
- [ ] AWS account → IAM → create user with `s3:PutObject` + `s3:GetObject` + `s3:ListBucket` on the replication bucket
- [ ] Add to local `.env`:
  - `AWS_ACCESS_KEY_ID=AKIA…`
  - `AWS_SECRET_ACCESS_KEY=…`
  - `AWS_REGION=us-west-2`
  - `LITESTREAM_S3_BUCKET=polarsharp-tenant-backups`

**Option C.3.b — Azure Blob Storage:**
- [ ] Azure Portal → Storage Account → create + capture key
- [ ] Add to local `.env`:
  - `AZURE_STORAGE_ACCOUNT_NAME=polarsharpbackups`
  - `AZURE_STORAGE_ACCOUNT_KEY=…`

**Option C.3.c — Google Cloud Storage:**
- [ ] GCP → Service Accounts → create with `Storage Object Admin` role
- [ ] Download service account JSON key
- [ ] Add to local `.env`: `GCP_CREDENTIALS_JSON_PATH=/secure/path/to/service-account.json`

**Option C.3.d — SFTP server:**
- [ ] Provision SFTP server (Hetzner Storage Box, AWS Transfer, etc.)
- [ ] Add to local `.env`:
  - `LITESTREAM_SFTP_HOST=…`
  - `LITESTREAM_SFTP_USER=…`
  - `LITESTREAM_SFTP_PRIVATE_KEY_PATH=…`

- **Will be used by:** `src/PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite/Litestream/` (config generator real; CLI scaffolded as TASK-V20-017)

### C.4 SSO providers (Phase 3 — per-tenant identity federation)

PLAN.md Phase 3 mentions per-tenant SSO with per-provider packages. None of the packages exist yet (they're future-scoped). When they ship, each provider needs:

- [ ] **Google** — Google Cloud Console → APIs & Services → Credentials → OAuth 2.0 Client ID
- [ ] **Microsoft** — Azure Portal → App registrations → New registration
- [ ] **Facebook** — developers.facebook.com → Create App → Facebook Login product
- [ ] **Apple** — developer.apple.com → Certificates, Identifiers & Profiles → Sign in with Apple
- [ ] **GitHub** — github.com/settings/developers → New OAuth App
- [ ] **LinkedIn** — linkedin.com/developers → Create app
- [ ] **X (Twitter)** — developer.twitter.com → App → OAuth 2.0
- [ ] **Snapchat / Pinterest / TikTok** — provider-specific dev portals (defer until Phase X.x ships)

KeyCloak provider (already shipped at `src/PolarSharp.MultiTenant.Identity.KeyCloak/`) needs no external creds; OIDC config + client ID/secret are per-tenant supplied at runtime.

---

## Services you must NOT acquire credentials for

Per **DECISIONS.md D-001 (Polar.sh as Merchant of Record)**:

- ❌ **Stripe** — PolarSharp does NOT integrate with Stripe directly. Merchants connect their bank in Polar.sh's dashboard; PolarSharp's role is deep-link generation + status polling.
- ❌ **Square / PayPal / Adyen / Braintree / any payment processor** — all payments route through Polar.sh's MoR coverage.
- ❌ **Direct banking / ACH APIs / Plaid** — routes through Polar.sh `/v1/organization/{id}/banking` endpoints.

Reject in code review any PR that adds direct credentials for these services. They violate the locked architectural decision.

---

## CI secret provisioning checklist

Once a credential is acquired, register it in GitHub Actions repository secrets so the CI Integration job picks it up:

```sh
# Example (DO NOT echo the value into the shell — paste into pbcopy first, then pipe):
pbpaste | gh secret set ANTHROPIC_API_KEY --repo MollsAndHersh/Polar.sh_Nuget
pbcopy < /dev/null   # clear clipboard immediately after
```

(Memory note: `feedback_secret_rotation_via_pbpaste.md` captures this pattern so secrets never enter the AI assistant's context window.)

For multi-value services (Azure OpenAI, Twilio), set each piece as its own secret. The CI workflow `.github/workflows/ci.yml` Integration step exposes them as env vars via `env: KEY: ${{ secrets.KEY }}`.

---

## Audit prompt template (for next time)

When new integration packages land, refresh this list by dispatching an Explore subagent with this prompt:

> "Audit `/Users/mollsandhersh/Repos/Polar.sh_Nuget` for every 3rd-party service the codebase integrates with. For each, produce a row: Service | What it does | Package(s) | Status (REAL/SCAFFOLD/ABSTRACTION) | Env var(s) needed | Where referenced (file:line). Sweep AI providers / Polar.sh / storage+CDN / shipping / tax / search / databases / SSO / email-SMS / Marten / monitoring / payment-processor-references (should be none per D-001) / Litestream / any other 3rd-party SDK references in Directory.Packages.props. Cite paths + line numbers. Output a markdown table grouped by priority (acquire-now / acquire-when-phase-lands / optional). Don't speculate."
