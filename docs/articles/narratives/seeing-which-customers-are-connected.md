# Seeing which customers are connected

> Most things a merchant wants to know about their customers come straight out of a regular database — "how many orders did Alice place last month," "which products are selling best this week." But some questions are about *connections between customers* — "which of our customers know each other," "is this new account suspiciously similar to a known fraudster," "if we mail a coupon to our top 100 buyers, who else will hear about it." Those questions are awkward to ask of a regular database. PolarSharp ships a separate piece — the **customer graph** — to answer them properly. This narrative walks through what it is, when to reach for it, and what to watch out for.

## What this narrative is about (and what it isn't)

This narrative is about the **customer-graph feature** in PolarSharp — the optional, opt-in subsystem that builds a network-shaped picture of the merchant's customers and lets you ask connection-style questions about them.

It is **not** about the regular customer list. The merchant's catalog database already has a `Customer` table; the customer graph is a separate, complementary thing that sits alongside it. You can run PolarSharp without the customer graph entirely — most simple stores will.

It is **not** about Polar.sh's own internal data structures. Polar.sh has its own customer concept which PolarSharp consumes via webhooks; the graph is built inside *your* application's data plane, from those webhook deliveries combined with your own database changes.

It is **not** about how to do social-network analysis on your customers as a marketing exercise. The graph is designed for very specific, narrow questions — fraud detection, look-alike analysis, sharing-of-account detection. Broad-strokes customer analytics belong in the [Advanced Reporting](../advanced-reporting.md) subsystem instead.

## A quick mental model: tables vs. graphs

If you have ever played the party game "six degrees of Kevin Bacon," you have used a graph database without knowing it. The game works because actors are connected to other actors by the films they worked on, and you can walk from any actor through a chain of co-stars to any other actor in surprisingly few hops.

A **graph database** stores data the same way that game thinks about Hollywood: as **nodes connected by edges**. Each customer is a node. Each fact that relates two customers (they ordered from the same IP, they live in the same city, they bought a gift card for each other, they are tagged with the same internal label) is an edge between two nodes.

Compare that to a **regular relational database**:

> Suppose you wanted to ask "which other customers ever ordered from the same IP as customer X?" in a regular database. You would query the orders table for customer X to find their IPs, then query the orders table again for every other customer to see if any IPs overlap, then group, then deduplicate. It takes several SQL statements and the cost grows with your customer count. The relational database was not designed for the question.
>
> In a graph database, the same question is **one hop**: "from customer X, follow every edge that says 'ordered from this IP', then follow every reverse edge back to customer nodes that share those IPs." The graph database was built for exactly this. The query is short, fast, and stays fast even when you have a million customers.

Think of a regular database like **a filing cabinet** — drawer for orders, drawer for customers, drawer for products, and to answer a relational question you open multiple drawers and cross-reference by hand. Think of a graph database like **a corkboard with strings between pins** — you can trace any chain of connections visually because the connections themselves are a first-class thing.

PolarSharp ships both. The catalog data lives in the filing cabinet (your relational database — SQLite, Postgres, etc.). The customer graph lives on a separate corkboard (currently Neo4j, with other providers planned). The two are kept in sync by a **projector** that listens for changes in the cabinet and updates the corkboard.

## What lives in the graph

Inside the graph, each customer is a node carrying a small bundle of facts:

- Their customer id (matching the row in the relational database)
- Their lifetime spend so far
- Their geo-resolved city and country (if available)
- A set of host-defined tags ("active," "vip," "fraud-flagged," "subscribed-to-newsletter," whatever the merchant uses)

And each customer is connected to other customers (and to other entities) by edges like:

- **`BOUGHT_FROM_IP`** — customer X has placed an order from IP address Y. (Only present if the tenant has IP capture enabled — see the IP-capture narrative.)
- **`PURCHASED_PRODUCT`** — customer X has bought product Z.
- **`LIVES_IN`** — customer X is geo-resolved to city / country C.
- **`TAGGED_AS`** — customer X carries tag T.

Through these edges, the questions that were awkward in the relational world become natural:

- *"Who shares an IP with this known fraud account?"* → start at the fraud account, follow `BOUGHT_FROM_IP` edges to IPs, follow reverse `BOUGHT_FROM_IP` edges back to other customers.
- *"Which customers in Berlin who bought our espresso machine also spent more than $500 lifetime?"* → start at the espresso-machine product node, follow `PURCHASED_PRODUCT` reverse to customers, filter by `LIVES_IN Berlin` and `LifetimeValue > 500`.
- *"What is the lifetime-value distribution of customers tagged 'vip'?"* → trivial — one tag-edge walk.

## A worked example

Suppose Olivia, the bakery owner from the IP-capture narrative, wants to investigate the seven suspicious orders. Last narrative she identified the IP, but she wants to go deeper: are there *other* customers in her store who have also ordered from that IP? If yes, are they a known good customer who got their card stolen, or do they look like co-conspirators?

Here is the query Olivia's application would build:

```csharp
var query = new CustomerGraphQuery()
    .WhereSharesIpWith("fraud-2026-04")     // the suspicious customer
    .WhereTaggedAs("active")                // narrow to currently-active accounts
    .OrderByLifetimeValue(descending: true) // biggest spenders first
    .Top(100);                              // cap the result set

var result = await graphClient.ExecuteAsync(query, ct);
```

The graph translator turns this into the appropriate native query for whichever graph database is configured underneath (Neo4j's openCypher in this case, Cosmos Gremlin in others), runs it, and hands back a typed result envelope of customer nodes with their fields populated.

Olivia gets back: three customers, of which two she recognises as established good customers (oh — that IP is a shared coffee shop), and one is a brand-new account she has never heard of. She tags the new account `fraud-flagged` and adds a manual review step. The investigation took thirty seconds.

The same investigation against a relational database would have taken several SQL statements and probably ended in an Excel pivot table.

## Three audience tiers — same query, different answers

PolarSharp's customer-graph queries are scoped to **audience tiers**. Each tier sees a different slice of the graph, and the same query asked from different tiers returns different results:

- **SaaSAdmin tier** — the person running the PolarSharp platform itself (you, the SaaS founder). Can see graph data across all tenants — useful for global fraud-pattern detection ("are there fraud rings spanning multiple tenants"). Very few people should have this tier in production.
- **Tenant tier** — the merchant operating one of the tenants. Sees only their own tenant's customers. This is the most common tier; it is what Olivia uses when she investigates her own bakery's customers.
- **Customer tier** — a particular customer themselves, asking about their own data ("show me everyone in my household sharing this account"). Sees only edges where they are one of the endpoints.

This is **audience-scoped schema slicing** in action — the LLM-driven natural-language query feature (covered separately) uses the same audience-tier idea, and the graph is the visible end of that architecture. Per [Case Study 04 "Audience-Scoped Schema Slicing"](https://github.com/MollsAndHersh/Polar.sh_Nuget/blob/main/Case%20Studies/04-Audience-Scoped-Schema-Slicing.md), the audience constraint is **structural** — a query issued at the Customer tier physically cannot see another customer's data, because the audience slice does not include the edges that would reveal it.

## How the graph stays current

The graph would not be very useful if it lagged behind the merchant's actual catalog by hours or days. PolarSharp keeps it fresh through a piece called the **customer-graph projector** (`ICustomerGraphProjector`), which lives in the `PolarSharp.CustomerGraph.Projection` package.

The projector listens for three streams of change:

1. **Polar webhooks** — every time Polar.sh sends a webhook about a customer event (an order, a refund, a subscription start), the projector receives the same event and updates the relevant graph nodes and edges.
2. **EF Core save-changes events** — every time the merchant's own catalog database commits a transaction that touches customer data, the projector hears about it via an EF Core interceptor.
3. **IP capture events** — when an order arrives with a `CustomerTransactionContext` carrying an IP address (and the tenant's `IpCaptureMode` is not `Disabled`), the projector adds the `BOUGHT_FROM_IP` edge to the graph.

The projector runs as a hosted background service — it does not block the request that triggered the change. There is a small delay (typically under a second) between a relational-database commit and the corresponding edge appearing in the graph. For fraud detection this is fine; for hard real-time uses, the graph is not the right tool.

If the projector falls behind or a deploy interrupts it, it can rebuild from scratch by replaying recent webhooks and walking the catalog database — the graph is **derived state**, not the system of record. Losing the graph and rebuilding it is annoying but not catastrophic.

## Which graph database underneath?

PolarSharp's customer-graph subsystem is abstracted away from any particular graph database. The `ICustomerGraphQueryClient` interface defines the contract; provider packages plug in actual graph engines.

In v1.3, the supported provider is **Neo4j**, via `PolarSharp.CustomerGraph.Neo4j`. Neo4j is the industry-standard graph database — open-source community edition runs anywhere, the enterprise edition adds clustering and multi-database support.

The Neo4j provider auto-detects which Neo4j edition it is connected to at startup and picks the appropriate tenant-isolation posture:

- **Neo4j Enterprise (multi-database)** — each PolarSharp tenant gets its own dedicated Neo4j *database*. Tenant A's customer graph and Tenant B's customer graph live in physically separate Neo4j databases that do not share an address space. Cross-tenant queries are **structurally impossible** because the databases cannot see each other. This is the strongest possible isolation posture and matches PolarSharp's defense-in-depth philosophy.
- **Neo4j Community (single database)** — all tenants share one Neo4j database, but every node carries a `:Tenant_<id>` label, and every query the translator builds includes a `MATCH` clause filtering by the tenant label. Weaker isolation than the Enterprise mode (relies on query-building discipline rather than physical separation) but works on the free tier.

On startup, the provider logs a clear Warning when falling back to label-based isolation so operators know they are on the weaker posture. Switching from Community to Enterprise is just a config-and-server-edition change — the application code does not move.

Other graph providers are planned. Per [Case Study 01 "Lift-And-Shift Architecture"](https://github.com/MollsAndHersh/Polar.sh_Nuget/blob/main/Case%20Studies/01-Lift-And-Shift-Architecture.md), the abstraction is intentionally provider-shaped so that a future Cosmos DB Gremlin API provider, or an in-process graph for tests, can drop in without touching the application code.

## When to reach for the graph (and when not to)

**Reach for the graph when:**

- You are doing **fraud-detection or shared-account detection** — questions like "who shares an IP with this account" or "what other accounts have shipped to the same address."
- You are doing **look-alike or affinity analysis** — "find me customers similar to these top spenders" or "who bought Product A and is in city B."
- You are walking **multi-hop relationships** — "friends of friends of customers who tipped a creator" — that would be deep joins in SQL.

**Stick with the relational database (and Reporting) when:**

- You want **aggregates over the whole customer base** — "average order value last month," "revenue per category." These belong in Advanced Reporting; the graph is not optimised for sweeping aggregates.
- You want **simple per-customer lookups** — "show me Alice's last 10 orders." The catalog database already has this; the graph adds nothing.
- You are **not yet at the scale** where the relational equivalent of a graph query becomes uncomfortable. The graph is opt-in for a reason — there is a real operational cost to running Neo4j, and a small store will not benefit. If your seven-orders-from-the-same-IP investigation runs fine as a SQL query in 30 milliseconds, the graph is overkill.

## Things to know

Plain-language gotchas, optional settings, and edge cases. Scan as needed.

- **The graph is opt-in. Without `PolarSharp.CustomerGraph` + a provider package, it does not exist.** The rest of PolarSharp works fine without it. Most merchants will not need the graph until they are large enough to have fraud-pattern questions or operations sophisticated enough to want look-alike analysis.

- **The graph requires IP capture to be enabled for the IP-related predicates to do anything.** The `WhereSharesIpWith` predicate matches on `BOUGHT_FROM_IP` edges, which only exist when the tenant's `IpCaptureMode` is `CaptureHashed` or `CaptureRaw`. If the tenant is on `Disabled`, the predicate returns nothing because there are no IP edges in the graph. See the [IP-capture narrative](tracking-where-your-customers-shop-from.md) for the trade-offs.

- **The graph is eventually consistent with the relational database.** A new order shows up in the relational catalog immediately on commit; in the graph it shows up a moment later when the projector catches up. For fraud detection this delay does not matter (fraud rings are not detected in the first 200 milliseconds anyway). For hard real-time use cases, do not rely on the graph being current to the millisecond.

- **The graph is derived state. You can rebuild it from scratch.** If the graph database becomes corrupted, falls behind too far, or you decide to switch providers, the projector can rebuild from recent webhook history and the relational catalog. You will not lose anything that was not in those two sources to begin with.

- **Multi-database Neo4j is the recommended production posture.** If your merchant base is large enough to need a customer graph at all, it is large enough to justify Neo4j Enterprise. The defense-in-depth tenant isolation that multi-database provides matches the rest of PolarSharp's posture; falling back to label-based isolation should be treated as a temporary or development-only configuration.

- **The natural-language-query feature in PolarSharp can drive graph queries too.** The `PolarSharp.NaturalLanguageQuery.CustomerGraph` package wires the LLM-driven query translator to the customer graph, so an operator can type "show me customers in Berlin who spent over €500 in the last 30 days" in English and get the same `CustomerGraphQuery` you would have written by hand. This is scaffold-level in v1.3 and ships fully in a later release.

- **Audience tiers are enforced at query-translation time, not at result-filter time.** When a query is built for the Customer tier, the translator literally cannot produce a query that walks edges across tenants — the audience constraint is baked into the query, not applied afterwards. This is the structural guarantee that makes multi-audience queries safe to expose to end users.

- **Tags are host-defined free-form strings.** PolarSharp does not have a built-in vocabulary for tags. The merchant's application code is responsible for choosing tag names and applying them consistently — "active," "vip," "newsletter-subscribed" are conventions, not enums. The graph stores whatever strings you give it.

- **Graph queries respect tenant isolation in single-tenant mode too.** Single-tenant deployments have exactly one `:Tenant_<id>` label; the query translator still adds the filter; the result is correct. There is no separate single-tenant code path.

- **Currently the graph is in scaffold form in v1.3.** The abstractions and the Neo4j provider's surface area are in place; the full projector implementation and complete query coverage land incrementally. Check the per-package READMEs for the current implementation status.
