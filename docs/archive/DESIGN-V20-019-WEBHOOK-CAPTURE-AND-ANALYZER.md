# DESIGN-V20-019 — Webhook payload capture + offline analyzer

**Status:** Proposal awaiting project-owner review.
**Author:** Claude (Opus 4.7).
**Last updated:** 2026-05-14.
**Scheduled:** v2.0 Pillar 2 (production hardening), shipping alongside RLS DDL (V20-012), audit interceptor cross-context dual-write, and server-side query translation (V20-014).

---

## Why this exists

PolarSharp's webhook tests today use **synthetic-but-cryptographically-real payloads** — we construct a JSON body, HMAC-sign it with the test secret using Polar's documented algorithm, POST it to an in-process `TestServer`. The signing is byte-identical to Polar's spec, but the payloads originate locally. The gap: **we never confirm that the JSON Polar actually sends matches what our `WebhookXxxData` records expect**. Polar can ship a new field, drop one, change an enum value, or introduce a new event type, and our synthetic tests would stay green because they're testing themselves.

Receiving an actual webhook from Polar's sandbox in CI requires a publicly-reachable URL (ngrok tunnel, replay service, etc.) — solvable but operationally heavy.

**The proposal sidesteps that entirely:** production deployments are already a real corpus of Polar payloads. Capture them. Replay against our handlers in CI. Flag drift.

## What it gives you

After a host turns on capture against their production (or sandbox) webhook stream for any window — even a few hours — they get:

1. **A coverage report.** For each `WebhookXxxData` record we ship, are there captured samples? "We have a handler for `subscription.uncanceled` but have never received one in real traffic" (might be a no-op handler that should be removed) vs "We've received 47 of these — our handler dispatches cleanly."
2. **Schema-drift detection.** JSON properties Polar sent that aren't in our record (Polar added a field — should we surface it?) and properties our record declares that never appear in real payloads (we may have over-modeled or Polar deprecated it).
3. **Unknown-event-type alerts.** `type` discriminators with no matching `WebhookXxxData` record. Polar shipped a new event since our last regen; we silently dropped it.
4. **Handler-coverage gaps.** Event types we have records for AND captured samples for, but no registered `IPolarWebhookHandler<T>`. Risk: silent drops in production.
5. **Field-value statistics.** Per-field null-rate, max string length, enum-value distribution. Catches "field documented nullable but never actually null in 10k samples" / "this enum has a 4th wire-format value our enum doesn't enumerate."

Wired into CI, the analyzer becomes a pre-publish gate that fails the build on schema drift.

## Architecture

### 1. Capture interface (core `PolarSharp.Webhooks` — new public surface)

```csharp
namespace PolarSharp.Webhooks.Capture;

/// <summary>
/// Optional capture sink invoked after HMAC verification but before handler dispatch.
/// The default <c>NoOpWebhookPayloadCapture</c> registration is zero-overhead; hosts
/// opt in by registering a concrete sink.
/// </summary>
public interface IWebhookPayloadCapture
{
    /// <summary>
    /// Captures one verified webhook payload. Implementations are expected to be
    /// non-throwing — capture failures must NOT interrupt handler dispatch. The webhook
    /// pipeline logs the failure at <c>Warning</c> and continues to the handler.
    /// </summary>
    Task CaptureAsync(WebhookCaptureContext context, CancellationToken ct);
}

public sealed record WebhookCaptureContext
{
    /// <summary>The event-type discriminator parsed from the payload's <c>type</c> field.</summary>
    public required string EventType { get; init; }

    /// <summary>The Polar event id (<c>evt_xxx</c>), captured for dedup.</summary>
    public required string PolarEventId { get; init; }

    /// <summary>The raw JSON body Polar transmitted, byte-identical (HMAC verified, no re-serialization).</summary>
    public required string RawJsonPayload { get; init; }

    /// <summary>Request headers as Polar sent them — useful for replay fidelity.</summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    /// <summary>UTC when the host's webhook endpoint received this request.</summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>Tenant context, if Finbuckle resolved one for the receiving request. Null for standalone-mode receivers.</summary>
    public string? CurrentTenantId { get; init; }
}
```

### 2. Three sink implementations as optional packages

| Package | Use case | Storage shape |
|---|---|---|
| `PolarSharp.Webhooks.Capture.FileSystem` | Local dev, single-pod hosts, low-volume | `{root}/{tenantId}/{eventType}/{yyyy-MM-dd}/{polarEventId}.json` |
| `PolarSharp.Webhooks.Capture.AzureBlob` + `.S3` | Cloud-hosted, high-volume, multi-pod | Same path shape, blob-storage backing |
| `PolarSharp.Webhooks.Capture.EntityFrameworkCore` (+ 3 providers) | Hosts that already have a DB and prefer to query captures via SQL | `WebhookPayloadCaptureEntity` table; subject to global tenant filter + RLS |

Each sink ships its own `Add<Sink>WebhookCapture()` extension on `PolarWebhooksBuilder`. Hosts pick one (or stack multiple — fan-out is the default if multiple `IWebhookPayloadCapture` are registered).

### 3. The analyzer

Ships as **`PolarSharp.Webhooks.Analyzer`** — both a library and a `dotnet polarsharp-webhook-analyze` CLI tool. Library API:

```csharp
public interface IWebhookCorpusAnalyzer
{
    /// <summary>Walks a captured corpus and returns the structured analysis report.</summary>
    Task<WebhookCorpusReport> AnalyzeAsync(IWebhookCorpusSource corpus, CancellationToken ct);
}

public interface IWebhookCorpusSource
{
    IAsyncEnumerable<WebhookCaptureContext> EnumerateAsync(CancellationToken ct);
}

public sealed record WebhookCorpusReport
{
    public required IReadOnlyList<EventTypeCoverage> Coverage { get; init; }
    public required IReadOnlyList<SchemaDriftFinding> Drift { get; init; }
    public required IReadOnlyList<string> UnknownEventTypes { get; init; }
    public required IReadOnlyList<HandlerGap> HandlerGaps { get; init; }
    public required IReadOnlyDictionary<string, FieldValueStats> FieldStats { get; init; }
    public required CorpusSummary Summary { get; init; }
}

public sealed record EventTypeCoverage(string EventType, int SampleCount, bool HasRecord, bool HasHandler);
public sealed record SchemaDriftFinding(string EventType, string Field, SchemaDriftKind Kind, string Description);
public sealed record HandlerGap(string EventType, int SampleCount);
public sealed record FieldValueStats(int TotalSamples, int NullCount, double NullRate, int MaxLength, IReadOnlyDictionary<string,int> EnumDistribution);
public sealed record CorpusSummary(int TotalSamples, int DistinctEventTypes, DateTimeOffset OldestSample, DateTimeOffset NewestSample);
```

Sink sources for analysis: file-system directories, EF Core query against the database sink, blob enumeration.

CLI:

```bash
dotnet polarsharp-webhook-analyze /var/lib/polar/captures \
    --output ./drift-report.json \
    --fail-on-unknown \
    --fail-on-drift
```

CI integration via the `--fail-on-*` flags lets the analyzer act as a pre-publish gate.

### 4. Corpus-replay test pattern

A new `tests/PolarSharp.Webhooks.Tests/Corpus/` directory holds anonymised captured samples committed to the repo. Each sample replays through the full pipeline (validator → dispatcher → handler) as a parameterised test:

```csharp
[Theory]
[ClassData(typeof(CorpusSampleProvider))]
public async Task Captured_payload_dispatches_through_pipeline(WebhookCaptureContext sample)
{
    var pipeline = BuildPipeline();    // signature verification skipped (corpus is pre-verified)
    var outcome = await pipeline.DispatchAsync(sample);
    Assert.True(outcome.IsHandled, $"{sample.EventType} dispatched without handler");
}
```

Adding a new sample = dropping a file into `Corpus/`. CI runs the analyzer against this directory; unknown event types or schema drift fail the build.

### 5. Privacy (mandatory; this section determines whether the design ships at all)

Webhook payloads carry **customer PII** — emails, names, billing addresses, sometimes ip addresses, sometimes payment-method metadata. The capture story has to default to safe.

**Defaults:**

- All sink packages register as **disabled by default**. Hosts opt in via config: `PolarSharp:Webhooks:Capture:Enabled = true`.
- Even when enabled, only **explicitly-listed event types** capture: `PolarSharp:Webhooks:Capture:EventTypes = ["order.created", "customer.created", ...]`. Empty list = no capture.
- The existing `IPolarPiiRedactor` (used today for log redaction) is invoked on the raw JSON BEFORE write. Redaction is automatic for: customer email (replaced with `cus_<hash>`), customer name (replaced with `[redacted]`), street/city/postal address (replaced with `[redacted]`). Configurable additional fields via `PolarSharp:Webhooks:Capture:AdditionalRedactedFields`.
- **Retention policy** in config: `PolarSharp:Webhooks:Capture:RetainDays` (default 7). The FileSystem sink ships an `IHostedService` that prunes captures older than the retention window once daily. EF sink ships an equivalent prune job.
- **Fingerprint-only mode** for hosts that want schema-drift detection but zero raw-payload retention: `PolarSharp:Webhooks:Capture:FingerprintOnly = true`. Stores the JSON-Schema-style shape (field names + types + null-rate) per event-type per day, never values. Indefinite retention is safe in this mode.

**Repo-committed corpus samples** (`tests/PolarSharp.Webhooks.Tests/Corpus/`) MUST be synthesized — never copy a real payload in. The samples use: `cus_test_<sequence>`, `synthetic@example.test`, `123 Test St / Test City / 12345`, amounts in round-number cents.

A documentation article in `docs/articles/webhook-payload-capture.md` covers the privacy rules explicitly so operators don't accidentally enable raw capture in production without understanding.

## Implementation phases

| Phase | Scope | Estimate |
|---|---|---|
| **2A** | `IWebhookPayloadCapture` interface in core + `NoOpWebhookPayloadCapture` default + pipeline integration (capture invoked post-verify, pre-dispatch) + `WebhookCaptureContext` record + opt-in config plumbing | 2 hours |
| **2B** | `PolarSharp.Webhooks.Capture.FileSystem` package — write impl, prune `IHostedService`, integration test | 2 hours |
| **2C** | `PolarSharp.Webhooks.Capture.EntityFrameworkCore` base + 3 SQL providers — entity + config + 3 migrations + prune job | 4 hours |
| **2D** | `PolarSharp.Webhooks.Analyzer` library — coverage report, schema-drift via reflection against `WebhookXxxData` records, unknown-event detection, handler-coverage check, field-value stats | 4 hours |
| **2E** | `dotnet polarsharp-webhook-analyze` CLI tool + JSON output + `--fail-on-*` exit codes for CI gating | 2 hours |
| **2F** | Corpus-replay test pattern + initial synthesized fixture set (≥1 sample per `WebhookXxxData` type) + CI workflow step that invokes the analyzer | 2 hours |
| **2G** | `PolarSharp.Webhooks.Capture.AzureBlob` and `.S3` — optional, cloud-host packages. Lower priority than 2A–2F | 3 hours |
| **2H** | Documentation: `docs/articles/webhook-payload-capture.md` (privacy + setup + retention) and `docs/articles/webhook-corpus-analysis.md` (analyzer usage + CI integration) | 1 hour |

Total v2.0 budget: ~16 hours for 2A–2F + 2H (the must-have set). 2G adds ~3 hours and can defer to v2.x.

## What's intentionally out of scope

- **Real-time analyzer / drift alerting** — the analyzer runs in CI on a corpus, not as a streaming watcher on live traffic. v2.x consideration; would need a separate `IWebhookDriftMonitor` that runs in-process.
- **Multi-pod capture deduplication** — same as the V20-005 design's distributed-dedup caveat: if two pods receive the same webhook (Polar retry), both capture it. The FileSystem sink dedups by `polarEventId` filename; the EF sink dedups by unique-index. The AzureBlob / S3 sinks rely on the `polarEventId`-suffix path being idempotent. Acceptable.
- **Replay-to-Polar** — some webhook providers offer to re-send historical events. Out of scope; this design only captures locally and replays through PolarSharp's own dispatcher.
- **PII tokenization / format-preserving encryption** — for hosts that want to KEEP customer-identifying capture but in a deterministic-pseudonymous form (so they can join across captures without leaking real values). v2.x consideration; would extend the redactor with a tokenizer interface.

## Open questions for project owner

1. **Should fingerprint-only mode be the DEFAULT** when capture is enabled, with raw-mode requiring an additional explicit opt-in? My lean: yes. Two opt-ins for raw capture (enable + raw-mode), one for fingerprint-only. Makes "I accidentally enabled capture and now I'm storing customer emails" basically impossible.
2. **Should the analyzer's CI gate fail the build on schema drift, or just warn?** My lean: warn by default (`--fail-on-drift` is opt-in). Schema drift is interesting signal but often non-urgent (Polar adding a nullable field doesn't break anything until we want to surface it). Unknown event types are a stronger signal — those default to `--fail-on-unknown=true`.
3. **Should the database sink be its own package or fold into the existing `PolarSharp.Webhooks.EntityFrameworkCore` (which doesn't exist yet)?** My lean: own package. Webhook capture is conceptually distinct from any future "webhook subscription state in EF" work, and the per-resource isolation keeps the dep tree minimal.
4. **Corpus sample anonymization — automated or manual?** When operators contribute samples to the repo, they need to be sanitized first. Options: (a) manual review checklist before commit (high friction, easy to forget), (b) automated `dotnet polarsharp-webhook-anonymize` CLI that ingests a raw capture and produces a synthesized version preserving the schema, (c) require all `Corpus/` samples to be hand-built from scratch rather than derived from real captures. My lean: option (b), automated tool, with the document strongly recommending its use AND a CI check that scans for obvious PII patterns (real-looking emails, real-looking phone numbers) and fails on detection.

---

## Approval prompt

If approved as written, this gets added to `~/TASKS.md` as **TASK-V20-019** with phases 2A–2H broken out, and gets implemented as part of v2.0 Pillar 2 production hardening (after the current V20-005 Phase 1B–1H per-resource live wirings complete). Estimated 16 hours work for 2A–2F + 2H; can ship 2A + 2B + 2D + 2F as a "v2.0-preview-3" milestone if you want intermediate progress visibility.

Redirect anything you want different and I'll revise. Otherwise just say "approved" and it gets logged into the v2.0 backlog.
