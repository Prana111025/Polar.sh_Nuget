# Coordination note — Phase 20 scope additions

**From**: main coordination session
**To**: wallet agent on `agent/wallet/phase-20-event-store`
**Date**: 2026-05-20
**Status**: read this BEFORE finalizing event-record signatures; the changes are additive but committing to them now avoids a painful schema migration later.

---

## Summary

A tax-architecture review in the main session surfaced two additive requirements that need to land in Phase 20's event signatures because **events are immutable once shipped — retrofitting these later would require a per-tenant schema migration on every wallet ledger.** Better to commit to them now even though the consumer (a later Phase 22.5 "Wallet Tax Responsibility" framework) doesn't ship yet.

You are NOT being asked to build the tax framework. You ARE being asked to size your event records so the framework can be built on top of your work without breaking anything.

---

## Change 1 — Funding-source provenance on every funding/credit/debit event

### Why

In wallet-only customer checkout mode (and the SaaS-revenue equivalent in Settlement Mode D from PLAN amendment 6), Polar.sh is bypassed entirely. The merchant — tenant or SaaS — becomes the legal taxpayer for those bypassed transactions. The tax obligation depends on the **funding source** of the tokens being spent: customer-cash-funded tokens are typically fully taxable on the new sale, but tenant-issued reward tokens are often treated as discounts that reduce taxable basis.

For the Phase 22.5 estimator to compute tax correctly, every debit needs to carry the breakdown of "which buckets of tokens are being spent and how much from each." That information has to be PRESENT ON THE EVENT — recomputing from event history isn't viable at scale.

### What to add

Three event-record changes:

**1. `WalletFunded` — add `Source` (single value per funding event)**

```csharp
public sealed record WalletFunded(
    WalletId WalletId, long SequenceNo, DateTimeOffset OccurredAt,
    Guid ActorUserId, Option<string> SourceIpHash, Option<string> IdempotencyKey,
    TokenAmount Amount,
    FundingSourceKind Source)              // NEW
    : WalletEvent(WalletId, SequenceNo, OccurredAt, ActorUserId, SourceIpHash, IdempotencyKey);
```

**2. `WalletCredited` — add `Source` (single value per credit event)**

Same shape addition. Credits are also funding-like events (admin issuance, promotional grants, etc.) and need source tagging for tax treatment.

**3. `WalletDebited` — add `FundingSources` (array; computed via allocation strategy)**

```csharp
public sealed record WalletDebited(
    WalletId WalletId, long SequenceNo, DateTimeOffset OccurredAt,
    Guid ActorUserId, Option<string> SourceIpHash, Option<string> IdempotencyKey,
    TokenAmount Amount, DebitTarget Target,
    IReadOnlyList<FundingSourceAllocation> FundingSources)   // NEW
    : WalletEvent(WalletId, SequenceNo, OccurredAt, ActorUserId, SourceIpHash, IdempotencyKey);

public sealed record FundingSourceAllocation(
    FundingSourceKind Kind,
    long Tokens,                            // how many tokens of this bucket were spent in this debit
    Guid? OriginatingFundingEventId);       // optional back-reference to the WalletFunded/WalletCredited that originally created these tokens
```

### `FundingSourceKind` enum (define in Abstractions)

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FundingSourceKind
{
    /// <summary>Tokens funded by customer paying through Polar / Stripe / PayPal at funding time.
    /// Tax treatment: typically fully taxable on the new sale.</summary>
    CustomerCashFunded,

    /// <summary>Tokens loaded into wallet via gift-card activation.
    /// Tax treatment: typically fully taxable on the new sale (redemption is the taxable event,
    /// per most US states' gift-card rules).</summary>
    GiftCardActivation,

    /// <summary>Tokens credited back to wallet from a prior refund (refund-as-credit).
    /// Tax treatment: typically fully taxable on the new sale (it's just customer's own money returned).</summary>
    RefundAsCredit,

    /// <summary>Tokens issued as tenant rewards / loyalty credits / promotional grants.
    /// Tax treatment: typically reduces taxable basis (discount); jurisdiction-dependent.</summary>
    TenantPromotionalGrant,

    /// <summary>Tokens issued by tenant operator as compensation for a service issue or bug.
    /// Tax treatment: typically reduces taxable basis (discount); jurisdiction-dependent.</summary>
    TenantBugFixCompensation,

    /// <summary>Tokens issued as a trial / signup bonus.
    /// Tax treatment: typically reduces taxable basis (discount).</summary>
    TrialCredit,
}
```

### Allocation strategy for debits (default behavior)

When a debit happens, the aggregate needs to figure out which funding-source buckets to spend FROM. The aggregate must track remaining-balance-per-bucket internally (project from the funding/credit/debit event history) so that at debit time it can output the correct `FundingSourceAllocation` array.

**Default strategy for Phase 20: FIFO (first-in-first-out).** The other strategies (pro-rata, specific-identification) ship with Phase 22.5 and require config plumbing that isn't worth doing now. FIFO is the simplest and predictable.

Implementation sketch (in the aggregate):

```csharp
private List<RemainingBucket> _bucketsFifo = new();  // appended in funding-event order

public WalletDebited Debit(TokenAmount amount, DebitTarget target, /* ... */)
{
    var allocations = new List<FundingSourceAllocation>();
    var remaining = amount.Tokens;
    var bucketIndex = 0;
    while (remaining > 0 && bucketIndex < _bucketsFifo.Count)
    {
        var bucket = _bucketsFifo[bucketIndex];
        if (bucket.RemainingTokens == 0) { bucketIndex++; continue; }
        var fromThisBucket = Math.Min(remaining, bucket.RemainingTokens);
        allocations.Add(new FundingSourceAllocation(bucket.Kind, fromThisBucket, bucket.OriginatingEventId));
        bucket.RemainingTokens -= fromThisBucket;
        remaining -= fromThisBucket;
        if (bucket.RemainingTokens == 0) bucketIndex++;
    }
    if (remaining > 0) throw new InsufficientWalletBalanceException(/* ... */);
    return new WalletDebited(/* ... */, FundingSources: allocations);
}
```

The bucket-state projection is part of aggregate state (reconstructible from event replay). On snapshot, the bucket list snapshots with everything else.

---

## Change 2 — Wallet events queryable by `tenant_id × date-range × amount`

### Why

The Phase 22.5 tax framework needs to aggregate wallet revenue across:
- All wallets for a given tenant (for tenant-perspective tax reports)
- All tenants (for SaaS-perspective tax reports — the SaaS's own revenue from per-tenant cuts)

Across arbitrary date ranges (quarterly, annual). With per-jurisdiction breakdowns (jurisdiction stamped at debit time per Change 1 above + additional stamping in Phase 22.5).

### What to ensure (mostly already true)

Your EF Core event-store implementations should already include indexes covering:

```
ix_wallet_events_tenant_id_occurred_at  (tenant_id, occurred_at DESC)
```

If your current implementation doesn't have this index (it's possible the schema only indexes by `wallet_id` + `sequence_no`), add it explicitly to each provider's migration. The Phase 22.5 aggregation queries will be slow without it.

The Marten implementation should index its events on `tenant_id` similarly (Marten supports computed-index declarations on event metadata).

### What NOT to do

- Do NOT add tax-computation logic to your event-store. That's Phase 22.5's job.
- Do NOT add tax-amount fields to events. Those get stamped in Phase 22.5 via a projection that reads your events + the per-tenant tax config + the jurisdictional rate table.
- Do NOT add a `tax_jurisdiction` column to events. That gets computed downstream from customer location data passed via `IWalletTransactionContext`.

---

## Confirmation request

Reply in your next checkpoint with one of:

- **"Acknowledged; integrated into Phase 20 event signatures"** — if you can land these two changes within your current scope
- **"Acknowledged but scope-extending; please advise"** — if integrating these would materially expand Phase 20 beyond the documented 30-50 source file budget
- **"Question: [...]"** — if any of the above is ambiguous given what you've already built

The wallet bridges (Phase 22) and the WTR framework (Phase 22.5) will both consume what you ship — but neither blocks Phase 20 from merging. You can finish Phase 20 as planned with these two additions and the rest of your scope unchanged.

---

## What else is changing (FYI, no action required from you)

The main session is also:
- Updating PLAN.md with the full "Wallet Tax Responsibility (WTR) framework" design
- Saving a memory note `project_wallet_tax_responsibility_framework.md`
- Removing TaxJar from v1.4.0 launch scope (per a user decision); keeping `IStorefrontTaxProvider` abstraction
- Adding a "Polar.sh as Merchant of Record" framing section to PLAN.md (closes a documentation gap)
- Posting NO coordination notes to agents B (storefronts) or C (docs); their scopes aren't affected by this work

Continue with your Phase 20 implementation. These two additions (FundingSources on events + tenant_id index) are the only things from this review that touch your active scope.
