# Manual steps for browser-only providers

These providers can't be fully automated — sign-up requires email verification, phone verification, or credit-card capture in a web browser. Once you have an API key, paste it into `.env` and run `./scripts/credentials/sync-to-github-secrets.sh --only PROVIDER_KEY_NAME` to fan it out to CI.

Each section below has the minimal click-path and the `.env` keys to populate.

---

## Anthropic Claude

1. Go to <https://console.anthropic.com/>
2. Sign up (email verification required)
3. Settings → API Keys → "Create Key" → name it `polarsharp-test`
4. Copy the key (`sk-ant-…`)
5. Add to `.env`:
   ```
   ANTHROPIC_API_KEY=sk-ant-…
   ```
6. Sync to CI: `./scripts/credentials/sync-to-github-secrets.sh --only ANTHROPIC_API_KEY`

**Cost:** $5 free credit on signup. Single-field translation tests use <$0.01 per run.

---

## OpenAI (if not using Azure OpenAI)

1. Go to <https://platform.openai.com/api-keys>
2. Sign in or create account
3. "Create new secret key" → name `polarsharp-test`
4. Copy the key (`sk-…`)
5. Add to `.env`:
   ```
   OPENAI_API_KEY=sk-…
   ```
6. Sync to CI: `./scripts/credentials/sync-to-github-secrets.sh --only OPENAI_API_KEY`

**Cost:** $5 free credit on new accounts. Test runs cost ~$0.001 per call against gpt-4o-mini.

**Note:** if you're using Azure OpenAI (via `provision-azure.sh`), you may not need this — Azure OpenAI gives you OpenAI's models via Azure billing. Some live tests still hit the OpenAI-branded endpoint; check which you need.

---

## xAI Grok

1. Go to <https://console.x.ai/>
2. Sign up (requires X account or email)
3. "API Keys" → create key
4. Copy the key (`xai-…`)
5. Add to `.env`:
   ```
   GROK_API_KEY=xai-…
   ```
6. Sync to CI: `./scripts/credentials/sync-to-github-secrets.sh --only GROK_API_KEY`

**Cost:** $25 free credit on signup; test runs are negligible.

---

## Google Gemini

Gemini's API key creation is fully scriptable via `gcloud`, but most users find the AI Studio path easier:

1. Go to <https://aistudio.google.com/app/apikey>
2. Sign in with Google
3. "Create API key" → select your project (or create one)
4. Copy the key
5. Add to `.env`:
   ```
   GEMINI_API_KEY=…
   ```
6. Sync to CI: `./scripts/credentials/sync-to-github-secrets.sh --only GEMINI_API_KEY`

**Cost:** Free tier is generous (15 RPM, 1M tokens/day) — test runs stay well within.

---

## SendGrid (email notifications)

1. Go to <https://app.sendgrid.com/>
2. Sign up (free tier: 100 emails/day)
3. Verify a sender identity:
   - Single sender (easiest): Settings → Sender Authentication → Single Sender Verification
   - Domain auth (for production): more involved, requires DNS records
4. Create API key: Settings → API Keys → Create API Key with "Mail Send" permission
5. Copy the key (`SG.…`)
6. Add to `.env`:
   ```
   SENDGRID_API_KEY=SG.…
   ```
7. Sync to CI: `./scripts/credentials/sync-to-github-secrets.sh --only SENDGRID_API_KEY`

**Cost:** Free for under 100 emails/day. Test runs use 1-2 emails per CI cycle.

---

## Twilio (SMS notifications)

1. Go to <https://www.twilio.com/try-twilio>
2. Sign up (requires phone verification + email)
3. Free trial gives you a phone number + $15 credit
4. Console dashboard shows Account SID + Auth Token
5. Buy a number (or use the trial number): Phone Numbers → Manage → Buy a number
6. Add to `.env`:
   ```
   TWILIO_ACCOUNT_SID=AC…
   TWILIO_AUTH_TOKEN=…
   TWILIO_FROM_PHONE_NUMBER=+1…
   ```
7. Sync to CI: `./scripts/credentials/sync-to-github-secrets.sh --only TWILIO_*`

**Cost:** Free trial covers ~50 test SMS. After that, ~$0.0075 per SMS in the US.

---

## TaxJar (sales tax — Phase 14.x; only if pursued)

1. Go to <https://www.taxjar.com/api/>
2. Sign up (free sandbox + 30-day trial of production)
3. Dashboard → Account → API Keys
4. Copy sandbox key (sandbox keys begin with `sandbox_`)
5. Add to `.env`:
   ```
   TAXJAR_API_KEY=…
   ```

**Note:** the WTR framework's `BasicEstimatedTaxCalculator` ships free in PolarSharp; TaxJar is an upgrade path. Skip unless real tenant demand materializes.

---

## EasyPost / Shippo (shipping — Phase 14.x)

Both have free test-mode keys.

**EasyPost:** <https://www.easypost.com/signup> → Dashboard → API Keys → copy test key (`EZTK…`) → `.env` as `EASYPOST_API_KEY`.

**Shippo:** <https://goshippo.com/> → API → copy test token (`shippo_test_…`) → `.env` as `SHIPPO_API_KEY`.

---

## Bulk-add to GitHub Actions secrets

Once everything's in `.env`:

```sh
./scripts/credentials/sync-to-github-secrets.sh
```

This pushes every known key from `.env` to repository secrets so CI's Integration job can run live tests.

To verify:

```sh
gh secret list --repo MollsAndHersh/Polar.sh_Nuget
```
