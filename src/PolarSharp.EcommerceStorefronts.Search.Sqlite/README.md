# PolarSharp.EcommerceStorefronts.Search.Sqlite

SQLite FTS5-backed implementation of `IStorefrontSearchProvider`. Pairs with the default per-tenant SQLite catalog deployment so storefronts get real full-text search without standing up a separate search service.

## Status

**Scaffold** (`IsPackable=false`). Real implementation lands in a follow-up phase. See `TASKS.md` (TASK-V14-???.search-sqlite) for the build-out plan.

## When to choose this

Pick `Search.Sqlite` when:

- The tenant's catalog already lives in SQLite (PolarSharp's default per-tenant DB choice)
- The catalog is small-to-medium (~5K–50K products) — SQLite FTS5 stays fast in this range
- You want zero new infrastructure — search runs against the same `.db` file as the catalog
- You don't need vector embeddings / semantic ranking (those need Azure AI Search or a dedicated vector DB)

## When to choose something else

- **In-memory default (built-in to `PolarSharp.EcommerceStorefronts`)** — fewer than ~10K products and no facet aggregation needs; ships with storefront-core; no separate package install
- **`PolarSharp.EcommerceStorefronts.Search.PostgreSql`** — tenant already on Postgres; want tsvector/tsquery
- **`PolarSharp.EcommerceStorefronts.Search.Elasticsearch`** — Docker-hostable big-name OSS; richer faceting + typo-tolerance
- **`PolarSharp.EcommerceStorefronts.Search.AzureAiSearch`** — cloud big-name; semantic ranker + vector search; pairs with Azure OpenAI translation

## Planned API

```csharp
services.AddPolarStorefrontsCore();
services.UseSqliteStorefrontSearch(databasePath: tenantDbDirectory);
```

The extension method registers `IStorefrontSearchProvider`, applies the FTS5 virtual-table migration on first run, and wires a `SaveChangesInterceptor` on the catalog DbContext so product inserts / updates keep the FTS index in sync.

## Tenant isolation

Search inherits the catalog's per-tenant-`.db`-file isolation — each tenant's FTS5 index lives in their own database file. No cross-tenant query is physically possible.
