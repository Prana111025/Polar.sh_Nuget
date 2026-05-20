# PolarSharp.EcommerceStorefronts.Search.PostgreSql

PostgreSQL `tsvector`/`tsquery`-backed implementation of `IStorefrontSearchProvider`. For tenants already running Postgres — search runs against the same database as the catalog; no separate search service required.

## Status

**Scaffold** (`IsPackable=false`). Real implementation lands in a follow-up phase. See `TASKS.md` (TASK-V14-???.search-postgresql) for the build-out plan.

## When to choose this

Pick `Search.PostgreSql` when:

- The tenant's catalog DB is Postgres (PolarSharp ships `EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL`)
- The catalog ranges from medium to large (~50K–500K products) — Postgres FTS scales well
- You want native full-text search with stemming, language-aware tokenization, and ranking
- You don't need vector / semantic ranking (those need pgvector extension or a dedicated vector DB)

## When to choose something else

- **In-memory default** — small catalogs, prototype work
- **`Search.Sqlite`** — per-tenant SQLite deployments
- **`Search.Elasticsearch`** — when you need richer faceting + typo-tolerance + multi-language analyzers beyond what Postgres FTS offers
- **`Search.AzureAiSearch`** — when you need semantic ranker + vector embeddings; pairs with Azure OpenAI

## Planned API

```csharp
services.AddPolarStorefrontsCore();
services.UsePostgreSqlStorefrontSearch(connectionString);
```

The extension method registers `IStorefrontSearchProvider`, adds the migration creating a `products_tsv tsvector` column + GIN index on the catalog tables, and wires a `SaveChangesInterceptor` that recomputes the tsvector on insert/update via `setweight(to_tsvector('english', coalesce(name,'')), 'A') || setweight(to_tsvector('english', coalesce(description,'')), 'B')`.

## Tenant isolation

Search inherits the catalog's tenant isolation:

- **Postgres RLS** (per DECISIONS.md D-005, when TASK-V20-008 lands) — the FTS query benefits from the same `tenant_isolation` policy as the catalog rows
- **EF query filter** (today's only line of defense for tenant scoping) — automatically appended to search queries
- **`tenant_id` index column on `products_tsv`** — composite GIN index `(tenant_id, products_tsv)` for efficient per-tenant FTS
