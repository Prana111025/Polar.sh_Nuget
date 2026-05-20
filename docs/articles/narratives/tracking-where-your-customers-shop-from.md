# Tracking where your customers shop from

> One of the quietest but most useful things PolarSharp can do for a merchant is keep a record of *where* their customers were when they placed an order — the IP address, the browser, the page they came from. That information is gold for spotting fraud and understanding your traffic. It is also one of the most regulated pieces of data on the internet. PolarSharp's job is to give each merchant a clean choice about how much to record, and to make it hard to capture something you did not mean to. This narrative walks through what gets captured, when, and how to pick the right setting.

## What this narrative is about (and what it isn't)

This narrative is about **per-tenant policy for capturing IP addresses, user-agent strings, and related request metadata** when customers transact through a merchant's storefront.

It is **not** about Polar.sh's own logging — Polar.sh has its own privacy policy and stores its own data; this narrative is purely about what PolarSharp records inside *your* application's database.

It is **not** legal advice. IP-address handling is regulated in many jurisdictions (GDPR in Europe, CCPA in California, LGPD in Brazil, etc.), and the right setting for any given merchant depends on where their customers are, what they have consented to, and what their lawyer says. PolarSharp ships a built-in advisory that flags settings that look non-compliant for the merchant's stated country, but the advisory is informational only. The merchant is responsible for the legal-compliance call.

It is **not** about cookies or browser-side tracking. PolarSharp does not set cookies or run JavaScript trackers on the merchant's storefront. Everything in this narrative is server-side: it only kicks in when the customer's request reaches the merchant's application.

## A quick mental model: why a merchant would want this at all

Suppose a merchant — let's call her Olivia, running a small bakery's online ordering site — wakes up to find that the same credit card has placed seven orders in the last hour. Three of them went to addresses in the same neighborhood; four went to addresses on the other side of the country. The pattern is suspicious enough to be worth a look.

Without an IP record, Olivia is detective-blind. She can see the names on the orders, the cards used, the shipping addresses — but she has no idea whether the seven orders came from one suspicious computer in a basement somewhere, or seven different customers who happen to share a card on a family plan, or one customer using their phone all day from different coffee shops.

**With an IP record, the picture becomes clear.** If all seven orders share one IP, that is one device fanning out fraudulent orders. If they all come from different cities, that is more likely real customers. The IP — plus the user-agent (which browser, which operating system) and the referrer (which page they came from) — gives Olivia just enough signal to triage.

This is the kind of decision PolarSharp wants merchants to be able to make based on data. The trade-off is that recording IP addresses is a non-trivial privacy choice, and it has to be done thoughtfully.

## The three modes, in plain language

Every tenant in PolarSharp picks one of three modes for IP capture, recorded in the tenant's business profile as `IpCaptureMode`. The three modes form a spectrum from "remember nothing" to "remember everything":

### Mode 1: `Disabled` — "I don't want to know"

This is the **default for new tenants** and it is the strongest privacy posture available. Nothing about the customer's request origin is captured. No IP, no user-agent, no nothing. Every PolarSharp service that takes a `CustomerTransactionContext` parameter receives `null` or an empty one, and the audit log records the action without any "where from" metadata.

Pick this mode if:

- You operate under a strict privacy regime (GDPR with no consent flow for IP processing, etc.).
- You have no plans to use fraud-detection features.
- You explicitly want to advertise to your customers that you do not log their network location.

The trade-off is that **fraud-detection features that depend on IP correlation do not work**. The `WhereSharesIpWith` predicate in the customer-graph query builder needs IP edges to exist in the graph; with this mode, no IP edges are created, so the predicate returns nothing.

### Mode 2: `CaptureHashed` — "I want fraud detection without the privacy load"

This is the **middle setting**, and it is genuinely clever.

Here is the idea: instead of storing the raw IP address, PolarSharp runs the IP through a one-way hash function (SHA-256) with a per-tenant salt, and stores **only the hash**. The original IP cannot be recovered from the hash. But — and this is what makes the mode work for fraud detection — the same IP, hashed with the same tenant's salt, always produces the same hash. So *within a single tenant's data*, you can still detect "these three orders came from the same IP" by noticing that they share a hash, even though you do not know what that IP was.

Think of it like **using nicknames at a coffee shop**. The barista does not know your name, but they recognise that the person who always orders the oat-milk flat white is one specific person, because they always show up the same way. The barista can spot patterns ("you've been in here three times this week"), but they cannot tell anyone *who you are*. The hash is the nickname — meaningful within one tenant, useless outside.

The salt is **per tenant**, which means Tenant A and Tenant B hashing the same IP get **different** hashes. So even though hashed IPs are stored, you cannot use them to correlate a customer's activity across multiple merchants — each merchant's hash space is its own world. That is intentional; it eliminates a cross-tenant tracking vector that simpler hashing schemes would have.

Pick this mode if:

- You want fraud-detection capability (spot the seven orders from the same source) but you want the underlying privacy story to be defensible.
- You are operating in a jurisdiction where IP addresses are considered personal data and you would prefer not to handle the raw values.
- You do not need geolocation or network-analysis features — only "same IP / different IP" comparisons.

The trade-off is that **you lose geolocation**. With only a hash, you cannot ask "which country did this order come from" — there is no IP-to-country lookup once you have only the hash. You also cannot share the data with downstream fraud-analysis services (most of them — Sift, MaxMind, the various risk-scoring vendors) because they all need the raw IP to do anything useful.

### Mode 3: `CaptureRaw` — "I want the full picture"

This is the **strongest fraud-detection posture**. The raw IP address is stored verbatim alongside the request. Geolocation lookups work. Network-analysis (same-ASN, same-subnet, anonymising-proxy detection) works. Sharing the data with a third-party fraud-detection service works.

This mode also activates the **retention policy**. Raw IPs are nullified — the row stays but the IP field is wiped — after a configurable window (default 90 days). The `IpRetentionPrunerService` runs as a background job and walks the database periodically, blanking out raw IPs older than the window. After the retention window, the row still exists for audit purposes, but the raw IP is gone.

Pick this mode if:

- You need real fraud-detection capability with geolocation.
- You are operating in a jurisdiction where IP capture is permitted with appropriate disclosures (e.g. you have a privacy policy your customers have agreed to that mentions network-location logging).
- You have configured a retention window that meets your jurisdiction's data-minimisation requirements.

The trade-off is that **the raw IP lives in your database for the retention window**. That is a real piece of personal data, and it deserves to be handled with care: backups must respect retention, access to the table must be audited, accidental log-line leaks (printing the IP to stdout) must be prevented.

## What gets recorded, exactly

When IP capture is enabled (either hashed or raw), every PolarSharp service call that affects a customer carries a `CustomerTransactionContext` object alongside the request. This object is the universal "request fingerprint" for the operation. It is **populated by the merchant's application code**, not by PolarSharp itself — the host application reads the request headers and passes the values in.

The fields on the context are:

- **`CustomerIp`** — the IP address, resolved according to the section below. This is the field that the tenant's `IpCaptureMode` actually governs; if the mode is `Disabled`, this gets dropped on the floor before storage. If the mode is `CaptureHashed`, it gets hashed before storage. If `CaptureRaw`, it gets stored verbatim.
- **`UserAgent`** — the `User-Agent` header, which says which browser and operating system the customer was using ("Mozilla/5.0 ... Chrome/130 ... Mac OS X 14.5"). Useful for fraud detection — most fraud rings cycle through a small set of bot user-agents.
- **`Referer`** — the `Referer` header (yes, that is the misspelling that has been in the HTTP spec since 1995), which says which page sent the customer to this one. Useful for traffic-source attribution.
- **`AcceptLanguage`** — the `Accept-Language` header, which says which languages the customer's browser prefers. Useful for locale resolution and for catching fraud where the language and the claimed country do not match.
- **`SessionFingerprint`** — an optional host-defined value (e.g. a browser-fingerprint hash from the host's analytics layer). PolarSharp does not generate this; it just stores it if the host populates it.

Once captured, the context flows through every customer-bound service: refunds, license validation, wallet operations, checkout completion, onboarding callbacks. Each service writes the relevant pieces into its audit-log entry.

## The "true IP" problem (and how PolarSharp solves it)

There is a real complication with IP capture that catches a lot of teams off guard.

When your application sits behind a load balancer, a CDN like Cloudflare, or a reverse proxy like Nginx, the IP address your application sees on every request is **not the customer's IP — it is the proxy's**. The customer's true IP is in a header called `X-Forwarded-For`, which the proxy adds before passing the request along.

Think of it like **passing a note in school through three friends to get to the back row**. The teacher (your application) sees the note arrive from the kid sitting next to her (the proxy). The actual sender (the customer) is at the back. To know who actually wrote the note, you have to look at the bottom of the page where each kid scribbled their name as they handed it on — that scribbled trail is `X-Forwarded-For`.

If your application just reads `HttpContext.Connection.RemoteIpAddress` (the obvious thing), every customer in the world looks like the proxy. Fraud detection becomes useless — every order shares the same "IP", which is the proxy's, and the seven-orders-from-the-same-IP signal triggers on every legitimate order too.

PolarSharp ships a small interface called `IClientIpResolver` to solve this. Two implementations are provided:

- **`DefaultClientIpResolver`** — reads `HttpContext.Connection.RemoteIpAddress`. Correct for hosts that are NOT behind a proxy. This is what you get if you do nothing.
- **`ForwardedHeadersClientIpResolver`** — reads `X-Forwarded-For`, walks the header value backwards through a configured list of *trusted* proxies, and returns the first untrusted IP. This is the correct customer IP. Required for hosts behind a proxy.

**You have to opt into the proxy-aware resolver explicitly.** The reason is security: trusting `X-Forwarded-For` without a proper trusted-proxy list is a hole — any client can send a fake `X-Forwarded-For` header and your application would believe it. PolarSharp defaults to the conservative behavior (do not trust the header) and asks the host to register the proxy-aware resolver only when actually behind a trustworthy proxy.

If you are behind Cloudflare, AWS CloudFront, Azure Front Door, or any load balancer, register `ForwardedHeadersClientIpResolver` with your proxy's IP range in the trusted list. If you are running directly on the public internet with no proxy, the default resolver is correct.

## The jurisdictional advisory

PolarSharp's wallet ships a small advisory that fires a Warning log entry when the tenant's stated country code combined with the chosen `IpCaptureMode` looks legally non-compliant. For example, a tenant whose `CountryCode = "DE"` (Germany) running `IpCaptureMode = CaptureRaw` will produce a startup warning along the lines of *"Raw IP capture is generally not permissible under GDPR without explicit consent — review your tenant's compliance posture."*

This is **informational only**. The advisory does not block the mode, does not change behavior, and is not a substitute for legal counsel. It exists to catch the obvious cases ("we forgot to switch to hashed mode when we expanded into the EU") at the time of configuration, not at the time of a regulator's letter.

If your merchant has done the legal work and the raw mode is appropriate for their flow (consent collected, privacy policy disclosed, retention configured), the advisory is just background noise. Silence it via your log filters; PolarSharp does not gate on it.

## Things to know

Plain-language gotchas, optional settings, and edge cases. Scan as needed.

- **The default is `Disabled` for new tenants.** PolarSharp will not record IPs unless the tenant has explicitly chosen a capturing mode. This is intentional — the safer default is "remember less."

- **You can change the mode at any time, but it only applies forward.** Switching from `Disabled` to `CaptureHashed` does not retroactively hash previously-empty IP records — those records had no IP to capture. Switching from `CaptureRaw` to `CaptureHashed` does not retroactively re-hash existing raw IPs — they sit at whatever they were captured as until the retention pruner blanks them. If you need to clean historical raw IPs, run the retention pruner with a shortened window or write a one-off migration.

- **Hashed mode uses a per-tenant salt that is generated once and stored on the tenant record.** Do not regenerate it — if you do, every previously-stored hash becomes uncorrelatable with future hashes (the same IP would hash to a different value), and your fraud-detection signal goes blank for the migration period.

- **The retention pruner only runs in `CaptureRaw` mode.** In hashed mode the stored value is already a one-way hash, so there is nothing to prune. The hash stays in the database indefinitely as part of the audit log — which is the same posture you would have with a non-PII identifier like an order number.

- **`CustomerTransactionContext.Empty` is the right value for non-customer-driven actions.** Background jobs (catalog publishes, scheduled billing runs, snapshot pulls) do not have a customer-facing request to draw a context from. Pass `CustomerTransactionContext.Empty` (or `null`) explicitly — the services accept both, and the audit log records the action without any "where from" metadata.

- **User-agent and referer have their own privacy considerations.** This narrative has focused on IP because that is the most regulated piece, but `User-Agent` is also a fingerprintable identifier. Most jurisdictions treat user-agent as less sensitive than IP, but if your privacy posture is conservative, consider blanking these too at the host layer before they reach PolarSharp.

- **Behind multiple proxies, configure the trust list carefully.** If you are behind Cloudflare AND your own internal load balancer, the trusted-proxy list must include both. The resolver walks `X-Forwarded-For` from right to left, skipping trusted entries, and returns the first untrusted one. Get the list wrong and you will either trust the wrong IP (security hole) or attribute every request to your own load balancer (fraud-detection blind spot).

- **PolarSharp does not log IPs to stdout, even in raw mode.** The internal logging is structured and the IP field is marked as sensitive — log sinks that support sensitive-data redaction will hide it by default. If you have a custom log sink, ensure you are not accidentally widening the surface.

- **Fraud-detection downstream features rely on this.** The customer-graph's `WhereSharesIpWith` predicate, future anomaly-detection rules, and the IP-correlation reports all draw from this data. If you have those features turned on but `IpCaptureMode = Disabled`, the features run but their queries return nothing — they have no data to correlate on.

- **There is no PolarSharp "IP geolocation lookup" feature built in.** PolarSharp captures the IP (in raw mode) but does not ship a geolocation provider. If you want to resolve "this IP is in Berlin," install a geolocation library separately (MaxMind, IPinfo, etc.) and query it at the application layer. PolarSharp's `IGeoIpResolver` interface is the integration point if you want to plug one in cleanly.

- **All of this is per-tenant, not per-installation.** Three tenants running on the same PolarSharp installation can each have a different `IpCaptureMode`. One can be `Disabled` (strict EU customer base), one `CaptureHashed` (US customer base, conservative), one `CaptureRaw` (B2B contracts where the merchant's own customers have signed network-logging clauses). They share the same code path; the mode just changes what happens at the storage layer.
