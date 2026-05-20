# PolarSharp.EcommerceStorefronts.Search.Elasticsearch

Elasticsearch / OpenSearch-backed implementation of `IStorefrontSearchProvider`. The "big-name OSS, Docker-hostable" option per the 2026-05-20 search-provider architecture decision.

## Status

**Scaffold** (`IsPackable=false`). Real implementation lands in a follow-up phase. See `TASKS.md` (TASK-V14-???.search-elasticsearch) for the build-out plan.

## When to choose this

Pick `Search.Elasticsearch` when:

- You want a battle-tested, mature search engine without cloud lock-in
- You're comfortable running a Docker container next to your app (or a managed Elastic Cloud / AWS OpenSearch instance)
- Catalogs of any size — Elasticsearch scales horizontally
- You need rich faceted search, typo-tolerance, multi-language analyzers, autocomplete
- You don't (yet) need semantic ranking / vector search — though Elasticsearch 8.x and OpenSearch 2.x both ship kNN vector search, the Azure AI Search bridge has tighter integration with Azure OpenAI for AI-driven discovery flows

## When to choose something else

- **In-memory default** — small catalogs, no infra wanted
- **`Search.Sqlite` / `Search.PostgreSql`** — when you don't want a separate search service at all
- **`Search.AzureAiSearch`** — cloud big-name; semantic ranker + vector + native Azure OpenAI integration

## Elasticsearch vs OpenSearch — pick one

This package will support both via a config switch:

- **Elasticsearch** (Elastic, Inc.) — original; commercial licensing (SSPL after 7.10); free for self-host; managed via Elastic Cloud
- **OpenSearch** (AWS-led fork) — Apache 2.0 licensed; managed via AWS OpenSearch Service

API-compatible for catalog search workloads; mostly differ in cloud-management story + licensing terms. The implementation lands with both vendor SDKs (`Elastic.Clients.Elasticsearch` for ES, `OpenSearch.Client` for OS) and a single options-driven selector.

## Docker dev setup

The `scripts/credentials/docker-dbs-up.sh` script will gain an `--with-elasticsearch` flag when the implementation lands:

```sh
docker run -d --name polarsharp-dev-elasticsearch \
  -p 9200:9200 -p 9300:9300 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  elasticsearch:8.15.0
```

## Planned API

```csharp
services.AddPolarStorefrontsCore();
services.UseElasticsearchStorefrontSearch(opts =>
{
    opts.Endpoint = new Uri("http://localhost:9200");
    opts.IndexNamePerTenant = true;   // recommended for tenant isolation
});
```

## Tenant isolation

Two modes:

1. **Index-per-tenant** (default; recommended): each tenant gets `storefront-{tenantId}-products` — physical isolation; no cross-tenant query is possible. Costs more resources at scale (Elasticsearch index overhead is real).
2. **Shared-index + filter** (configurable): single `storefront-products` index with `tenant_id` field on every document; queries auto-append a `term` filter on `tenant_id`. Cheaper but the tenant isolation layer is purely query-shaping (a bug in the bridge could leak rows). Mark with a warning in the README.

The cross-tenant isolation regression test (per the 5-layer tenant isolation acceptance criterion in DECISIONS.md D-005) must pass against both modes.
