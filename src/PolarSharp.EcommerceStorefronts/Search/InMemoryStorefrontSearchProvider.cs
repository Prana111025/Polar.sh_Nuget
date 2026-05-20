using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Catalog;
using PolarSharp.EcommerceStorefronts.Abstractions.Paging;
using PolarSharp.EcommerceStorefronts.Abstractions.Search;

namespace PolarSharp.EcommerceStorefronts.Search;

/// <summary>
/// Built-in default <see cref="IStorefrontSearchProvider"/> that delegates to the host's
/// <see cref="IStorefrontCatalogProvider"/> for substring matching and computes facets +
/// suggestions in-process. Ships in the storefront-core package so storefronts work the
/// moment a host calls <c>AddPolarStorefrontsCore()</c> — no external search service is
/// required.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Scope.</strong> Suitable for tenants with up to ~10,000 products. Above that
/// scale, the per-request enumeration cost (loading every product to compute facet counts
/// + suggestions) becomes the bottleneck. Tenants that outgrow this default upgrade to
/// one of the dedicated bridge packages:
/// </para>
/// <list type="bullet">
///   <item><see cref="IStorefrontSearchProvider"/> = <c>PolarSharp.EcommerceStorefronts.Search.Sqlite</c> — SQLite FTS5; matches the default DB choice; no new infra.</item>
///   <item><see cref="IStorefrontSearchProvider"/> = <c>PolarSharp.EcommerceStorefronts.Search.PostgreSql</c> — Postgres tsvector/tsquery; for tenants already on Postgres.</item>
///   <item><see cref="IStorefrontSearchProvider"/> = <c>PolarSharp.EcommerceStorefronts.Search.Elasticsearch</c> — Docker-hostable big-name OSS; faceted + typo-tolerant + relevance ranking.</item>
///   <item><see cref="IStorefrontSearchProvider"/> = <c>PolarSharp.EcommerceStorefronts.Search.AzureAiSearch</c> — cloud big-name; semantic ranker + vector embeddings; pairs with Azure OpenAI translation.</item>
/// </list>
/// <para>
/// <strong>Why this exists.</strong> Per the architectural decision locked 2026-05-20 (see
/// DECISIONS.md), searching MUST work out of the box without requiring tenants to configure
/// a cloud or self-hosted search backend. The cloud + OSS providers are upgrades, not
/// prerequisites. This in-memory provider closes the "do nothing and search still works"
/// gap.
/// </para>
/// </remarks>
public sealed class InMemoryStorefrontSearchProvider : IStorefrontSearchProvider
{
    private readonly IStorefrontCatalogProvider _catalog;

    /// <summary>Initialises the provider with the catalog source it'll delegate to.</summary>
    /// <param name="catalog">The host's catalog provider — typically a bridge implementation
    /// against <c>PolarSharp.EcommerceStoreManagement</c>'s authoring catalog or against
    /// Polar.sh's REST API.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
    public InMemoryStorefrontSearchProvider(IStorefrontCatalogProvider catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public async Task<StorefrontResult<PagedResult<StorefrontProduct>>> SearchAsync(
        SearchQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var listQuery = new ListProductsQuery
        {
            CategoryIds = query.CategoryIds,
            SearchText = query.Text,
            Language = query.Language,
            Page = query.Page,
            PageSize = query.PageSize,
            SortBy = query.SortBy,
        };

        return await _catalog.ListProductsAsync(listQuery, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<StorefrontResult<SearchFacets>> GetFacetsAsync(
        SearchQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Loading every matching row to count facets is acceptable at this provider's
        // documented scale (~10K products). For larger catalogs, swap in a dedicated
        // search bridge that aggregates server-side.
        const int MaxFacetSamplePageSize = 1000;
        var sampleQuery = new ListProductsQuery
        {
            CategoryIds = query.CategoryIds,
            SearchText = query.Text,
            Language = query.Language,
            Page = 0,
            PageSize = MaxFacetSamplePageSize,
            SortBy = query.SortBy,
        };

        var sample = await _catalog.ListProductsAsync(sampleQuery, ct).ConfigureAwait(false);
        return sample.Match(
            onSuccess: page =>
            {
                var products = page.Rows;
                var facets = new Dictionary<string, IReadOnlyList<SearchFacetValue>>();

                // Category facet — count occurrences of each category id across the sample.
                var byCategory = products
                    .SelectMany(p => p.CategoryIds)
                    .GroupBy(c => c)
                    .Select(g => new SearchFacetValue { Value = g.Key, Count = g.Count() })
                    .OrderByDescending(v => v.Count)
                    .ToList();
                if (byCategory.Count > 0)
                {
                    facets["category"] = byCategory;
                }

                // Tag facet — surface the top tag values across the matching products.
                var byTag = products
                    .SelectMany(p => p.Tags)
                    .GroupBy(t => t)
                    .Select(g => new SearchFacetValue { Value = g.Key, Count = g.Count() })
                    .OrderByDescending(v => v.Count)
                    .Take(20)
                    .ToList();
                if (byTag.Count > 0)
                {
                    facets["tag"] = byTag;
                }

                return StorefrontResult<SearchFacets>.Success(new SearchFacets { Facets = facets });
            },
            onFailure: err => StorefrontResult<SearchFacets>.Failure(err));
    }

    /// <inheritdoc/>
    public async Task<StorefrontResult<IReadOnlyList<string>>> SuggestAsync(
        string partial,
        int max,
        CancellationToken ct)
    {
        // Empty partial → no suggestions. Don't load the entire catalog for a single keystroke.
        if (string.IsNullOrWhiteSpace(partial))
        {
            return StorefrontResult<IReadOnlyList<string>>.Success(Array.Empty<string>());
        }

        var listQuery = new ListProductsQuery
        {
            SearchText = partial,
            Page = 0,
            PageSize = Math.Max(1, Math.Min(max, 50)),
        };

        var result = await _catalog.ListProductsAsync(listQuery, ct).ConfigureAwait(false);
        return result.Match<StorefrontResult<IReadOnlyList<string>>>(
            onSuccess: page =>
            {
                IReadOnlyList<string> suggestions = page.Rows
                    .Select(p => p.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(max)
                    .ToList();
                return StorefrontResult<IReadOnlyList<string>>.Success(suggestions);
            },
            onFailure: err => StorefrontResult<IReadOnlyList<string>>.Failure(err));
    }
}
