# Understanding prepaid wallets

> **Audience:** product owners, merchant operators, sales and support staff — anyone who needs to
> understand what the wallet feature does and when to use it without needing to read code.
> **Reading time:** about ten minutes.

## What the wallet actually is

Suppose a merchant sells small things often. Maybe it's an app that charges a few cents for an
API call, or a coffee shop loyalty program, or a tutoring service charging by the lesson. Every
time a customer makes a purchase, the payment processor takes a cut — and on a $0.50 transaction,
that cut can be larger than the merchant's margin. Charging twenty $0.50 transactions in a month
means paying twenty processing fees instead of one.

A prepaid wallet fixes that. The customer loads $25 onto the wallet *once* — one payment, one
processing fee — and then spends those funds over the month without the merchant paying any more
fees per purchase. The merchant keeps more of every sale; the customer doesn't see their card
charged every time they buy something small; everyone wins.

**Think of it like a hotel mini-bar tab.** The customer doesn't pull out a credit card each time
they grab a soda; everything goes on the room tab and gets settled once at checkout. The wallet
is the room tab — one payment up front, many small purchases against the balance.

## Where the design came from

Mark Chipman (the project owner) noticed two things while studying how prepaid systems work in
the real world:

1. Most wallet implementations track *only* the current balance and a history of "this much was
   added, this much was spent." When something goes wrong months later — a customer disputes a
   charge, an auditor wants a breakdown, a tax filing needs detail — the underlying numbers are
   gone. You can see the balance is $7.32 today but not how it got there.
2. The economics of who pays which fees are usually invisible. The customer's credit-card
   statement shows $25.00. The merchant's monthly settlement shows $23.81 net. Where did the
   $1.19 go? Most systems leave both sides to figure it out from separate documents.

So the PolarSharp wallet was designed to do two things differently. **It records every change as
an immutable event** (think of a bank statement that can never be edited, only appended to), and
**every funding event captures the full economic breakdown** (customer-charged amount, processor
fee, SaaS profit, tenant absorbed, tenant net, all in cents, all on the same record). Years
later, anyone with a question about the $25.00 the customer paid on Tuesday can pull up the event
and see exactly where every penny went.

## How "event sourcing" works (the friendly version)

The technical name for this design is "event sourcing." Don't worry about the name — the idea is
simpler than the name suggests.

**Think of it like a ship's log.** A ship's captain writes each event as it happens — "0800,
departed port; 1100, course change to 270 degrees; 1500, sighted whales." The log is never
rewritten. To figure out where the ship is right now, you start at the beginning and replay every
entry in order. That's it. The ship's current position is a *consequence* of the log, not a
separately-stored fact.

The wallet works the same way. The wallet's current balance isn't stored as a single number
that gets updated. Instead, every funding, debit, credit, refund, freeze, and close is appended
to a list. The balance is *computed* from the list by replaying the events. If you want to know
the wallet's balance on a particular Tuesday three months ago, you replay the list up to that
Tuesday and read off the balance. The history isn't "extra data" — the history *is* the data.

This sounds slow, but in practice it isn't. The system takes a "snapshot" of the wallet's state
every 50 events (think of a ship's captain noting the position in the margin every dozen
entries) so the replay only has to cover the events since the most recent snapshot. For a wallet
with 10,000 events ever recorded, loading the current state is still a fast operation.

## The "no surprises" promise

A wallet command — fund, debit, credit, refund, freeze, close — always tells you exactly what
happened. There are three possible outcomes:

1. **The command worked.** A new event was appended. The balance moved. You get back the event
   and the new balance.
2. **The command was rejected for an expected reason.** The wallet didn't have enough balance to
   debit, or the wallet is frozen, or the wallet is closed. The system returns a typed error
   you can pattern-match on — `InsufficientFunds`, `WalletIsFrozen`, `WalletIsClosed`,
   `WalletAlreadyExists`, `WalletNotFound`. No exception was thrown; the code path is the
   ordinary "did the command succeed?" check.
3. **Something went wrong with the infrastructure.** Two writers tried to update the same wallet
   at the same time, or the database went away. The system throws a real exception. These are
   genuinely exceptional, not "the customer asked for something I couldn't do."

This matters because it lets every business workflow handle wallet outcomes the same way it
handles any other yes-or-no decision: a clear branch in code, no try/catch ceremony for ordinary
"the customer is broke" cases.

## Why retries are safe

The internet is unreliable. Every payment processor on the planet has the same problem: you send
a request, you don't get a response, and now you don't know if the operation succeeded or not.
Did the wallet get funded twice? Did the debit go through? Should I retry?

The wallet solves this with **idempotency keys**. Every command carries a short string the caller
generates and reuses on retries. The wallet's event store remembers every key it has seen for a
given wallet. **If the same key shows up twice, the second one short-circuits to the original
result** — no duplicate funding, no double-debit, just the original event returned again. The
caller doesn't have to track "did I already do this?" — the wallet does.

Think of it like a coatcheck ticket. You hand in your coat, you get a ticket number. If you lose
the receipt for the coatcheck and come back asking again, the attendant sees the same number,
hands you the same coat — they don't accidentally take a second coat off the rack.

## What the wallet *doesn't* do (and why that matters)

The wallet is deliberately narrow in scope. It does **not**:

- **Talk to payment processors directly.** The wallet records that a funding event happened with
  $25.00 charged on a Polar.sh order; it doesn't itself call Stripe or PayPal. The funding
  processors are separate packages that turn payment-processor webhooks into wallet commands.
  This is why the wallet can be lifted out of PolarSharp later (see the "Things to know" section)
  — it's not entangled with the payment-processor implementation.
- **Decide tenant-billing economics.** Whether the tenant absorbs the processing fee, passes it
  through as a gross-up, or shows the customer a different token count is configured per-tenant
  in the tenant's business profile, not in the wallet itself. The wallet just records whichever
  numbers it's told about.
- **Send notifications.** The wallet appends events; downstream notification handlers (a
  separate package that ships in Phase 23) listen for the events and send email/SMS/UI toasts.
  The wallet's job ends when the event is recorded.
- **Decide whether a refund is allowed.** A refund is a command; if it succeeds, the tokens come
  off the wallet. The "should this refund be allowed?" policy lives in the bridge package that
  initiates the refund (Phase 22) — including the dormancy rules and surcharge math.

This narrowness is the design's strength: every concern lives in one place, and the wallet itself
stays small enough to reason about completely.

## Knowing where each token came from (and why it matters for taxes)

Every token in the wallet remembers where it came from. **Think of it like a piggy bank that
keeps the original deposit slip taped to each coin**: a quarter dropped in by Grandma at
Christmas knows it's a gift; a quarter the kid earned doing chores knows it's earnings; the
quarters look the same in the jar, but if you ever need to explain "where did the money come
from?" — say to a parent, or to a tax authority — the slips are still there.

This matters because different funding sources are treated differently for tax purposes. A
customer who pays $25 cash to load their wallet and then spends $10 on a purchase is being taxed
on the $10 sale, full stop. But a tenant who *gives* a customer $25 in promotional credit and
then the customer spends $10 of it is in a different situation entirely — most jurisdictions
treat the promotional credit as a discount that reduces the taxable basis of the sale, not as
revenue. To file the right tax forms later, the system needs to know which of the spent tokens
came from which kind of funding.

The wallet handles this by tagging every funding and credit event with a category — customer
cash, gift card, refund returned to wallet, tenant promotional grant, bug-fix compensation, or
trial credit. When tokens get spent, the wallet records exactly which categories the spent
tokens came from (oldest tokens go first — first in, first out — same as the gas in your car's
tank). The full tax-aware reporting framework lands later (the wallet itself doesn't do tax math
— that's a separate system being built next), but the *data* it needs is being captured now,
because the events are immutable once recorded. Adding it later would require a painful
ledger-wide migration; capturing it on day one is essentially free.

## Things to know

This is the section to scan if you're operating the wallet in a real deployment.

- **The wallet is single-currency.** Each wallet locks its currency at creation time. A customer
  who wants to fund in two currencies needs two wallets.
- **Frozen wallets reject funding and debits, but credits and refunds are allowed.** Freezing is
  for fraud holds; the operator might still need to manually credit a wallet (refund a disputed
  charge, give a goodwill credit) while it's frozen.
- **Closed wallets reject *all* commands.** Closure is terminal. Open a new wallet if needed; do
  not try to "reopen" a closed one. The event log of a closed wallet stays queryable for audit
  purposes; you just can't change it.
- **The 50-events-per-snapshot default is tunable.** Most hosts won't need to think about it.
  Hosts on Cosmos DB should configure the stride down to 10 because Cosmos charges per request
  unit; replaying long event streams is RU-expensive.
- **Idempotency keys are required, not optional.** Every command carries one. Callers that
  retry need to reuse the same key; callers that generate fresh keys per attempt will get
  duplicate appends (which is a bug). Most webhook handlers use the upstream provider's webhook
  delivery id as the key — it's already unique and already retried with the same value.
- **The wallet feature is "lift-safe."** The four Phase 20 packages have zero dependencies on
  any other PolarSharp package. That means the wallet could, at some future point, be extracted
  into its own repository and used by projects that don't use Polar.sh at all. The discipline of
  keeping it self-contained pays off in two ways: today, the wallet is genuinely separable and
  testable; tomorrow, if the project owner ever wants to ship the wallet as a standalone library,
  the extraction is a short script rather than a multi-week refactor.
- **Marten or EF Core — pick one.** If your database is Postgres, Marten is the right choice
  (native event-sourcing primitives). Anywhere else, use the EF Core provider that matches your
  database. They produce identical wallet behavior; the difference is purely about which storage
  primitives are doing the heavy lifting underneath.
- **Concurrency conflicts are normal and self-recovering.** A hot wallet under simultaneous
  funding + debits will occasionally produce concurrency-conflict exceptions; the MediatR retry
  behavior re-reads the latest state and re-applies the command up to three times before giving
  up. If you see a lot of these in metrics, the wallet may need to be sharded (multiple wallets
  per customer with weighted routing) — but that's a Phase 22+ concern.

## What's coming next

Phase 20 (this work) ships the spine: events, commands, queries, the aggregate, snapshots, and
the two storage backends. The richer features — Polar.sh funding, notifications, refunds with
dormancy math, balance escalation, B2B purchase orders — land in Phases 21 through 25 per the
project plan. Each of those phases will get its own Implementation Narrative explaining the
user-visible impact.

For a developer-oriented reference of the same material, see
[Prepaid wallets — event-sourced core](../articles/prepaid-wallets-event-sourcing.md).
