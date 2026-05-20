# Picking the right database for your PolarSharp app

> PolarSharp ships with five different database providers — SQLite, SQL Server, PostgreSQL, MariaDB / MySQL, and Azure Cosmos DB — and there is also a Postgres-native option called Marten for one specific feature (the prepaid wallet). That is a lot of choice, and the right answer is genuinely different depending on who you are and what you are building. This narrative walks through the trade-offs in plain language so you can pick once and not look back.

## What this narrative is about (and what it isn't)

This narrative is about **which database engine to put your PolarSharp app's data into**. Not how to install one. Not how to tune one. Not how to migrate from one to another. Just: *given five reasonable choices, how do you decide?*

It is **not** a database-administrator tutorial. Each of these engines has its own ecosystem of tooling, monitoring, backup tools, certification programs, and twenty-year-old gotchas, and we are not going to try to cover any of that. If you already have a strong opinion about your database engine — keep it. Skip this narrative and just install the matching PolarSharp provider package.

It is **also** not about ecommerce store data vs. reporting snapshots vs. wallet events vs. tenant registry. Internally PolarSharp has several subsystems and they each have their own provider package, but the *choice* is almost always the same: pick one database family and use it across the board. We will come back to the rare exceptions at the end.

## A quick mental model: what does the database actually do here?

Think of your application's database as **the filing cabinet behind the front desk** at a small hotel.

The hotel front desk handles guest requests — checking people in, looking up reservations, marking rooms clean, processing payments. The filing cabinet is what makes any of that survive past lunch break. Without the cabinet, every conversation evaporates the moment it ends; with it, the hotel remembers who is staying in room 312 and when their bill is due.

In a PolarSharp application, the database is exactly that filing cabinet. It holds:

- **Tenants** — the merchants who have signed up with you and use your application to run their own business. Think of them like franchisees. If you are running PolarSharp in single-tenant mode (just you), there is exactly one tenant; everyone else is a multi-tenant deployment with N tenants.
- **The catalog** — for each tenant, their products, their categories, their pricing tiers, the discount codes they have set up, the bank-account-not-yet-connected warning that hasn't gone away.
- **Reporting snapshots** — periodic captures of "what does this tenant's business look like right now" used to draw dashboards quickly without re-asking Polar.sh for the same numbers a hundred times a minute.
- **Audit logs** — the running diary of "who did what to which thing and when."
- **Wallet ledgers** — if you have turned on the prepaid-wallet feature, every top-up and every spend, in append-only form (so the running balance is always the sum of every event ever recorded against that wallet).

That is all your application's database does. PolarSharp itself does not store anything on Polar.sh's servers, and Polar.sh does not give you back anything you need to keep in your own database to function — every piece of long-lived state PolarSharp cares about lives in *your* database, the one you choose here.

## A second mental model: what makes the choice hard?

If they were all interchangeable we would not be writing this. The thing that makes the choice non-obvious is that **databases trade off between "easy to run" and "designed for big multi-tenant SaaS workloads"**, and that trade-off is roughly the spectrum we have to think about.

Imagine the spectrum like this:

> **Simple ←→ Industrial**

On the simple end: a single file on disk, no separate server process, no networking required, you copy the file as your backup, you are done. Brilliant for one machine; not designed for an entire datacenter.

On the industrial end: a giant managed cloud database, replicas in three regions, row-level security policies, point-in-time restore, automatic failover, a full-time DBA whose job is to keep the lights on. Wonderful for SaaS at scale; complete overkill if your application is going to be run by you and three of your friends.

PolarSharp supports the entire spectrum because **different deployments of the same PolarSharp app live in different places on it**. The same SaaS owner might run on SQLite locally for development, on Postgres in staging, and on SQL Server in production. The same library works across all of them. You just install a different provider package and change one line in your startup file.

Now let us walk through the five options.

## Option 1: SQLite — "I just want it to work on my laptop and one small server"

Suppose you are building a side-project SaaS and you want to ship the first version this weekend. You do not yet have customers. You do not want to set up a database server. You want to run the entire app on a single Linux box for under $20 a month and only think about scaling when you actually have a reason to.

**SQLite is the right answer.** SQLite is a database that lives inside a single file on disk. There is no "database server" running in the background — your application opens the file directly, reads from it, writes to it, and closes it. You back it up by copying the file. You give it more capacity by moving it to a bigger disk. That is the entire operations story.

In PolarSharp, the SQLite provider goes one step further: **each tenant gets their own file**. If you have three tenants signed up, the directory looks like this:

```
/var/lib/polarsharp/tenants/
├── master_SaaS.db        ← the platform (which tenants exist, when they joined, etc.)
├── tenant-aaa.db         ← Tenant A's catalog, customers, settings
├── tenant-bbb.db         ← Tenant B's catalog, customers, settings
└── tenant-ccc.db         ← Tenant C's catalog, customers, settings
```

This is **physical file isolation**. Tenant A's data and Tenant B's data are not in the same file. You can literally hand Tenant A a copy of their `.db` and walk away — there is no risk of accidentally leaking Tenant B's records.

SQLite is also small in a way most databases aren't. The whole engine is under a megabyte of code, has no daemon to monitor, no port to open, no password to manage, no version mismatches between client and server. People sometimes underestimate it because it is "just a file," but SQLite serves billions of users every day on phones, browsers, and embedded devices — it is genuinely production-grade for the right shape of workload.

### When SQLite is a great fit

- You are running on **one machine**, or a single VM behind a load balancer. (More than one writer at once on the same database file is where SQLite stops being a great fit.)
- You expect **low to moderate write throughput**. Reads on SQLite are extremely fast; writes are serialised, so very high write rates eventually bottleneck.
- You **value operational simplicity** over multi-region replication and seven-nines availability. There is no SQLite cluster to babysit; there is no SQLite version drift to coordinate.
- You want **per-tenant data isolation that is physically obvious** — every tenant lives in their own file, so a confused query cannot return the wrong tenant's rows because the wrong tenant's data is not even in the same file.

### When SQLite stops being a great fit

- You want **multiple application servers writing to the same data**. SQLite can be used across multiple readers, but writes need to be serialised — at scale, that means moving to one of the server-based databases below.
- You need **active-active replication across regions** for disaster recovery. SQLite has Litestream (which PolarSharp natively supports — see the article on Litestream replication) but Litestream is one-way: it streams your file to cloud storage so you can restore from there. It is not a multi-master setup.
- You need **fancy database-side authorization policies** like Postgres row-level security. SQLite has nothing equivalent. PolarSharp gives you app-layer tenant isolation regardless, but if you specifically want defense-in-depth at the database layer, this is not the option.

You install it with `dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite` and wire it up with `.UseSqlite("/var/lib/polarsharp/tenants/")` in your startup file. The matching reporting and ecommerce-management provider packages follow the same naming pattern (`PolarSharp.Reporting.EntityFrameworkCore.Sqlite`, `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Sqlite`).

## Option 2: PostgreSQL — "I want the serious one, run it myself"

Now suppose your SaaS is past the prototype stage. You have a handful of paying tenants, the writes are picking up, and you have decided you want a real database server — but you do not want to be locked into Microsoft's licensing model or stuck on a cloud-provider-specific service. You want something that runs on any Linux machine, has thirty years of community track record, and gives you all the bells and whistles a serious application is going to want eventually.

**PostgreSQL** (everyone says "Postgres") is the answer.

Postgres is the database that gets recommended whenever someone asks "I just want a really good relational database, what do I use?" It is free, open source, runs anywhere, has an enormous ecosystem of extensions, and is the database that most modern SaaS companies eventually end up using regardless of where they started.

Think of Postgres like **a really well-built central library**. There is a building (the server process). There is a staff (administrative tooling, monitoring agents, replication processes). There are rules about who can take which books out (database-level security policies). And there is institutional memory — Postgres backs up incrementally, you can replay any historical state, and you can replicate your data to one or many standby copies for disaster recovery.

PolarSharp's Postgres provider has one feature that the SQLite provider does not: **Row-Level Security (RLS)**. RLS is a Postgres feature that lets the database itself enforce "this row is only visible to this tenant." Even if your application code had a bug and forgot to filter by tenant ID, the database would silently refuse to return rows belonging to other tenants. This is **defense in depth**: the application enforces tenant isolation at the C# query level, AND the database enforces it again at the SQL level. Both have to fail simultaneously for a cross-tenant leak to happen.

### When Postgres is a great fit

- You are past the side-project stage and want a **proper database server** that can grow with you for years.
- You want **defense-in-depth tenant isolation** — Postgres's row-level security gives you a database-layer safety net beneath PolarSharp's application-layer query filters.
- You want **open-source freedom** — Postgres is BSD-licensed; you can run it anywhere, hosted or self-managed, with no per-instance fees.
- You want a **rich ecosystem** — every modern monitoring tool, backup tool, replication tool, ORM, and managed-database service supports Postgres.

### When Postgres might not be your first pick

- You are deeply embedded in the **Microsoft / Azure ecosystem** and your team already runs SQL Server everywhere. Picking Postgres just to be different will add operational friction. SQL Server is the right call in that world.
- You are deeply embedded in the **Azure-native serverless world** and the rest of your data is in Cosmos DB. Mixing relational + document-store engines is doable but adds cognitive overhead. Cosmos may be the better through-line.

You install it with `dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL` and wire it up with `.UsePostgres(connectionString)` in your startup file.

## Option 3: SQL Server — "we run on Microsoft, end of story"

Suppose you work at a company that has standardised on Microsoft tooling. Your apps run on Windows or .NET on Linux, your infrastructure is on Azure, your data warehouse is Azure Synapse, your monitoring is Azure Monitor, your incident-management is in Teams, your developers know SSMS keyboard shortcuts by heart. Going to Postgres would mean educating an entire org on a new dialect of SQL, new admin tooling, and new operational patterns — and you do not have a good reason to do that.

**SQL Server is the right answer.** PolarSharp's SQL Server provider is a complete peer to the Postgres one — same tenant store, same row-level security, same migration patterns, same defense-in-depth. The only thing that changes is which engine is doing the work underneath.

In particular, SQL Server has its own **row-level security** implementation (security predicates on tables), and PolarSharp's SQL Server provider configures it the same way the Postgres provider does. You get the same two-layer tenant-isolation guarantee on either engine.

### When SQL Server is a great fit

- Your **team and tooling are Microsoft-aligned** and you want to keep that consistency.
- You want **defense-in-depth tenant isolation** at the database layer.
- You are running on **Azure SQL** (the managed Azure-native variant) and want first-class integration with Azure Monitor, AAD authentication, automatic backups, geo-replication, etc.
- Your **operational know-how is in SQL Server** — DBAs who can read execution plans, find blocking sessions, and tune indexes are valuable; do not throw that knowledge away by picking a different engine just for fashion.

### When SQL Server might not be your first pick

- You want the engine to be **free and open-source** — SQL Server's free Express edition has size limits; the production editions are commercial. Postgres is free at every scale.
- Your hosting bill is **already too high on Azure**, and you are looking to move workloads to cheaper providers. Postgres is portable to anywhere; SQL Server is portable but the licensing follows you.

You install it with `dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer` and wire it up with `.UseSqlServer(connectionString)` in your startup file.

## Option 4: MariaDB / MySQL — "we're a LAMP-stack shop"

Suppose your company is rooted in the open-source web stack — Linux, Apache or Nginx, PHP or Node, and MySQL or MariaDB underneath. Your operations team has spent years tuning MySQL. Your monitoring dashboards are built around MySQL metrics. Migrating to Postgres would mean rebuilding all of that, and the cost is genuinely not worth it just to switch databases.

**MariaDB / MySQL is the right answer.** PolarSharp's MariaDB provider works against both MariaDB 10.5+ and MySQL 8.0+.

There is **one important catch** with this engine, though, and it is worth being explicit about: MariaDB and MySQL do not have a Postgres-equivalent row-level security feature. The tenant-isolation story on this engine is **application-layer only** — PolarSharp's EF Core global query filter enforces "always filter by tenant ID" on every query, but the database itself is not enforcing it as a second layer. If a developer wrote a raw SQL query that bypassed Entity Framework, the database would happily return rows from any tenant.

For most teams this is fine — the EF Core filter is solid, and "do not bypass EF Core for tenant data" is a normal, sensible coding rule. But if your security posture explicitly requires defense-in-depth at the database layer (because you have multiple tenants in the same database file or because compliance requires it), this is the engine where you do not get that for free. Picking Postgres or SQL Server gets you the second layer automatically.

### When MariaDB / MySQL is a great fit

- Your team **already runs MySQL / MariaDB** everywhere and you want to keep the operational story consistent.
- You are **single-tenant or low-tenant-count** — the lack of database-layer RLS matters less when you have one or a handful of trusted operators inside one database.
- You want the **open-source ecosystem** but specifically prefer MySQL's ecosystem over Postgres's.

### When MariaDB / MySQL might not be your first pick

- You are running a **multi-tenant SaaS with hard tenant-isolation requirements**. Without database-layer RLS, the application is your only line of defense. Postgres or SQL Server is the safer call.
- You want **rich JSON / document support** with full indexing. Postgres's JSONB is more mature here.

You install it with `dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb` and wire it up with `.UseMariaDb(connectionString)` in your startup file.

## Option 5: Cosmos DB — "we're Azure-native and document-shaped fits"

Suppose your application is built on Azure-native services — Azure Functions, Logic Apps, App Service, Event Grid — and you have leaned into the document-store data model rather than the relational one. Your data shapes change frequently. You want global distribution with per-region writes. You want millisecond latency at the 99th percentile no matter how big the dataset gets.

**Azure Cosmos DB is the right answer.** PolarSharp's Cosmos provider uses the SQL API (despite the name, this is Cosmos's JSON document API) and uses each tenant's id as the **partition key** — meaning Cosmos itself stores each tenant's data on its own logical (and often physical) partition, which is the cloud-document-store equivalent of SQLite's per-tenant `.db` file.

Cosmos is a **fundamentally different shape of database** from the four above. The other four are relational (rows + tables + foreign keys); Cosmos is a document store (JSON documents grouped into containers, looked up by partition key). PolarSharp models the same domain objects across both shapes — a `LocalProduct` is still a `LocalProduct` — but the storage layout underneath is genuinely different. Some operations that are easy in relational SQL (multi-table joins, ad-hoc aggregate queries) are awkward in Cosmos; some operations that are awkward in relational SQL (sharding across many regions, infinite horizontal scale) are easy in Cosmos. Pick this engine because the trade-off shape fits your application, not because you happen to be on Azure — the rest of PolarSharp works fine on Azure-hosted Postgres or SQL Server.

### When Cosmos DB is a great fit

- You are **building Azure-native** and the rest of your stack expects document-store semantics.
- You need **global distribution with single-digit-millisecond latency** and per-region write availability.
- You expect **very high partition counts** (thousands of tenants, each with their own partition) and want the storage engine to handle that natively.
- You are comfortable with a **document-store data model** and the relational mental model is not load-bearing for you.

### When Cosmos DB might not be your first pick

- You like **ad-hoc SQL**, cross-table joins, and analytical queries against your operational data. Cosmos's query language is SQL-flavoured but the optimisations are partition-key-shaped; cross-partition queries are slow and expensive.
- You are **cost-sensitive** at low scale. Cosmos's pricing model is request-units-per-second; for small workloads a t3.micro Postgres is enormously cheaper.
- You want **portability away from Azure**. Cosmos is Azure-only. Postgres, SQL Server, MySQL, and SQLite all run anywhere.

You install it with `dotnet add package PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb` and wire it up with `.UseCosmosDb(...)` in your startup file.

## A sixth option, just for one feature: Marten on Postgres

There is one more engine worth knowing about, even though it only applies to one specific feature.

**Marten is a library that turns PostgreSQL into a native event store.** Event sourcing is a pattern where, instead of storing "the current state of this thing," you store **every event that ever happened to it**, in append-only order, and you reconstruct the current state by replaying the events. PolarSharp's prepaid-wallet subsystem uses event sourcing because money is one of those domains where you genuinely want a complete audit trail: every top-up, every debit, every adjustment, every refund — recorded forever, never edited.

If your application already runs on PostgreSQL, you can opt to put the wallet's event store on Marten by installing `PolarSharp.PrepaidWallets.EventStore.Marten` and calling `.UseMartenWalletEventStore(connectionString)`. The wallet's event streams, projections, and snapshots all live in your existing Postgres database, managed by Marten's native event-streaming features. No separate database needed.

If you are not on Postgres, or if you prefer EF Core's storage abstractions, the wallet can use a regular EF Core event store instead — there are EF Core wallet event-store packages for SQLite, SQL Server, Postgres, MariaDB, and Cosmos. The wallet feature still works identically; only the storage choice differs.

**You do not need to mix engines.** If your tenant registry is on SQLite, your wallet can also be on SQLite. If your registry is on Postgres, your wallet can be on Postgres (either Marten or EF Core). The Marten option exists for shops that want native Postgres event-sourcing primitives; it is not the only way to use the wallet.

## The "should I mix providers" question

You can mix. PolarSharp does not assume the tenant registry, the ecommerce catalog, the reporting snapshots, and the wallet events all live in the same database — each subsystem has its own provider package, and each picks its provider independently in your startup file. Some teams do, in fact, run their reporting on a different engine from their transactional data.

But unless you have a specific reason to, **pick one engine and use it everywhere**. The reason is operational: one engine means one backup story, one monitoring dashboard, one set of failover procedures, one schema-migration tool. Two engines means twice as much operational surface to keep healthy, and the cognitive load shows up in incidents.

Common reasons to legitimately mix:

- You have a corporate Postgres for transactional data, and a Cosmos DB for reporting snapshots because the analytical query patterns are document-shaped.
- You have a SQL Server transactional database, and SQLite for local development.
- You want the wallet specifically on Marten (Postgres-native event sourcing) while everything else is on a different engine.

Outside of those cases, pick one and move on.

## Quick reference: provider package per engine

| Engine | Tenant store package | Wallet event-store package | Ecommerce catalog package | Reporting snapshots package |
|---|---|---|---|---|
| SQLite | `PolarSharp.MultiTenant.EntityFrameworkCore.Sqlite` | `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.Sqlite` | `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Sqlite` | `PolarSharp.Reporting.EntityFrameworkCore.Sqlite` |
| PostgreSQL | `PolarSharp.MultiTenant.EntityFrameworkCore.PostgreSQL` | `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.PostgreSQL` *or* `PolarSharp.PrepaidWallets.EventStore.Marten` | `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL` | `PolarSharp.Reporting.EntityFrameworkCore.PostgreSQL` |
| SQL Server | `PolarSharp.MultiTenant.EntityFrameworkCore.SqlServer` | `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.SqlServer` | `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer` | `PolarSharp.Reporting.EntityFrameworkCore.SqlServer` |
| MariaDB / MySQL | `PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb` | `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.MariaDb` | `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.MariaDb` | `PolarSharp.Reporting.EntityFrameworkCore.MariaDb` |
| Azure Cosmos DB | `PolarSharp.MultiTenant.EntityFrameworkCore.CosmosDb` | `PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.CosmosDb` | `PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.CosmosDb` | `PolarSharp.Reporting.EntityFrameworkCore.CosmosDb` |

The convention is rigorously consistent: one engine, one package per subsystem, named to match. Once you have picked an engine, the install commands are mechanical — replace the engine suffix in each package name and you are done.

## Things to know

Plain-language gotchas, optional settings, and edge cases. Scan as needed.

- **You can change your mind later.** Switching engines is real work — you have to export data from the old one and import it into the new one — but it is not architectural surgery. PolarSharp's abstractions are the same on every engine, so application code does not change; only your startup file's `.UseXxx(...)` line and the install command change.

- **Defense-in-depth tenant isolation is only available on Postgres and SQL Server.** SQLite gives you physical file isolation (different file per tenant), which is arguably even stronger. MariaDB / MySQL give you application-layer-only isolation. Cosmos gives you partition-key isolation. Each is reasonable for the right deployment; pick the one whose isolation story matches your compliance requirements.

- **Litestream replication is a SQLite-only feature.** If you pick SQLite and you want a backup story beyond "copy the file occasionally," PolarSharp natively supports Litestream — a tool that continuously streams your SQLite file's changes to cloud object storage (S3, Azure Blob, GCS, SFTP, or a local disk). See the configuration article for the full Litestream options.

- **The MariaDB / MySQL provider uses Oracle's MySQL.EntityFrameworkCore.** This is Oracle's official provider, not the community Pomelo provider. The two have slightly different behaviour around connection-string parsing and timezone handling; if you have existing migrations on Pomelo and switch to PolarSharp, expect to re-run migrations.

- **Cosmos's request-units pricing model can surprise you.** Cosmos charges per request, not per gigabyte-month, and the request cost depends on the query complexity. A poorly-indexed query in Cosmos can quietly become very expensive. PolarSharp's models are designed to be partition-key-aware (every tenant-bound query includes `tenantId`, which is the partition key), so you should not hit this for normal usage — but if you write your own ad-hoc cross-tenant analytics, watch the RU dashboard.

- **Single-tenant mode works on all five engines.** If you only ever have one tenant, the tenant-registry table just has one row in it. There is no separate "single-tenant build" of PolarSharp; the same code paths run, and the multi-tenant filter is a no-op when there is only one tenant. So picking a database does not lock you into one deployment shape — the same engine choice works whether you grow to one tenant or one thousand.

- **For local development, SQLite is a great default even when production is something else.** A common pattern is "Postgres in production, SQLite for the developer-laptop unit-test database." PolarSharp's abstractions are the same on both, so the test suite that runs against SQLite locally is also valid against Postgres in CI. Many teams run their CI matrix this way deliberately.

- **The choice does not have to be made on day one.** If you genuinely do not know, start with SQLite. It has the lowest friction, the cheapest hosting, the simplest backup story, and the fewest moving parts. When you outgrow it (and "outgrowing it" usually means "you have a second machine"), you migrate. Until then, do not over-build.

- **There is no PolarSharp lock-in to any engine.** Every provider package is a thin layer over Entity Framework Core (or Cosmos's native client, or Marten). Your data lives in the engine you picked, in tables you can inspect with any standard tool. If you ever decide to leave PolarSharp entirely, your data is still your data, in a normal schema, in a normal database. Per [Case Study 01 "Lift-And-Shift Architecture"](https://github.com/MollsAndHersh/Polar.sh_Nuget/blob/main/Case%20Studies/01-Lift-And-Shift-Architecture.md), the abstractions are designed for this.
