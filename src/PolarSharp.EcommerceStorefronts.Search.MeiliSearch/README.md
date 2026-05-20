# PolarSharp.EcommerceStorefronts.Search.MeiliSearch

> **⚠ DEPRECATED 2026-05-20.** This package is kept for back-compatibility with existing references but will not receive a real implementation. New deployments should pick one of these instead:
>
> - **`PolarSharp.EcommerceStorefronts.Search.Elasticsearch`** — Docker-hostable big-name OSS; mature ecosystem; rich faceting + vector + multi-language analyzers
> - **`PolarSharp.EcommerceStorefronts.Search.AzureAiSearch`** — cloud big-name; semantic ranker + vector + native Azure OpenAI integration
> - **`PolarSharp.EcommerceStorefronts.Search.PostgreSql`** — Postgres tsvector/tsquery; reuses your existing DB
> - **`PolarSharp.EcommerceStorefronts.Search.Sqlite`** — SQLite FTS5; matches the default per-tenant SQLite deployment
> - **In-memory default** (built into `PolarSharp.EcommerceStorefronts`) — no infra required; works out of the box for catalogs up to ~10K products
>
> **Why deprecated:** PolarSharp's architectural direction (2026-05-20 user decision) is to prefer hyperscaler-backed cloud providers + well-known OSS Docker images for third-party integrations. MeiliSearch is a fine product but lacks the name-recognition + "big-corp-survives-the-decade" guarantee that the project owner values. The above 5 alternatives cover every reasonable deployment shape without depending on Meilisearch's continued existence.

## Status

**Scaffold + DEPRECATED** (`IsPackable=false`). Will NOT receive a real implementation. Existing references should migrate to one of the alternatives above.

## Why the package wasn't deleted

Hard-deleting the csproj would break consumers who already added `<PackageReference Include="PolarSharp.EcommerceStorefronts.Search.MeiliSearch" />` in their host. Keeping the scaffold + clear deprecation note + a "use X instead" pointer gives those consumers a deterministic migration path.

When v2.0 ships, this package will be hard-deleted; until then it's a parking-spot.
