# PolarSharp.EcommerceStorefronts.Search.AzureAiSearch

Azure AI Search (formerly Cognitive Search) implementation of `IStorefrontSearchProvider`. The "cloud big-name" option per the 2026-05-20 search-provider architecture decision. Pairs naturally with `PolarSharp.EcommerceStoreManagement.Translation.AzureOpenAI` for end-to-end AI-driven product discovery (semantic ranker uses Azure OpenAI under the hood).

## Status

**Scaffold** (`IsPackable=false`). Real implementation lands in a follow-up phase. See `TASKS.md` (TASK-V14-???.search-azureaisearch) for the build-out plan.

## When to choose this

Pick `Search.AzureAiSearch` when:

- You're already on Azure for other services (Azure OpenAI translation, Cosmos DB tenant store)
- You want best-in-class search relevance — Azure AI Search ships semantic ranker (re-ranks results using a language model), built-in vector search (kNN with HNSW indexes), and hybrid retrieval (combines lexical BM25 with vector similarity)
- Catalogs of any size — Azure AI Search scales horizontally and pricing is predictable
- You want a Microsoft-supported managed service with SLA, automatic backups, and zero ops

## When to choose something else

- **In-memory default** — small catalogs, no cloud setup wanted
- **`Search.Sqlite` / `Search.PostgreSql`** — self-host, reuse existing DB
- **`Search.Elasticsearch`** — Docker-hostable big-name OSS; can run on-prem; no cloud bills

## Provisioning

The `scripts/credentials/provision-azure.sh` script provisions an Azure AI Search service alongside Azure OpenAI + Cosmos DB in a single resource group. Free tier covers 50 MB of data + 3 indexes + 10K documents — sufficient for dev and small-tenant production.

See `CREDENTIALS.md` § A.3 for the credential-acquisition checklist.

## Planned API

```csharp
services.AddPolarStorefrontsCore();
services.UseAzureAiSearchStorefrontSearch(opts =>
{
    opts.ServiceName = Environment.GetEnvironmentVariable("AZURE_SEARCH_SERVICE_NAME")!;
    opts.AdminKey = Environment.GetEnvironmentVariable("AZURE_SEARCH_ADMIN_KEY")!;
    opts.IndexNamePerTenant = true;   // recommended for tenant isolation; free tier allows 3 indexes so dev/test fits
    opts.UseSemanticRanker = true;    // engages Azure's LM-based re-ranker
    opts.UseVectorSearch = true;      // generates embeddings via the linked Azure OpenAI deployment
});
```

## Tenant isolation

Two modes (mirrors the Elasticsearch bridge):

1. **Index-per-tenant** (default; recommended for production): each tenant gets `storefront-{tenantId}-products` — physical isolation. Note the free tier's 3-index cap; bump to Basic tier ($75/month) to support more than 3 tenants in production.
2. **Shared-index + filter**: single `storefront-products` index with `tenant_id` field; queries auto-append a `$filter=tenant_id eq '{id}'` clause. Cheaper at scale but tenant isolation is query-shaping only.

## AI integration with Azure OpenAI translation

When both `Translation.AzureOpenAI` AND `Search.AzureAiSearch` are wired against the same Azure subscription, the search index can use the same OpenAI deployment for:

- **Translation-aware indexing** — index the canonical product description PLUS pre-translated versions for every supported language, so customers in any locale get matching results without query-time translation overhead
- **Vector embeddings** — Azure AI Search natively integrates with Azure OpenAI's `text-embedding-3-small` deployment for indexing-time embedding generation; kNN queries at search time hit the same deployment

This is the "AI-driven product discovery" capability that PolarSharp's `army-of-trending-marketplaces` deployment model relies on (per PLAN.md Phase 3).
