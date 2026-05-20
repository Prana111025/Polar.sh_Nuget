using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Catalog;
using PolarSharp.EcommerceStorefronts.Abstractions.Paging;

namespace PolarSharp.EcommerceStorefronts.Tests.TestSupport;

internal sealed class FakeCatalogProvider : IStorefrontCatalogProvider
{
    private readonly Dictionary<string, StorefrontProduct> _products = new();

    public FakeCatalogProvider WithProduct(StorefrontProduct product)
    {
        _products[product.Id] = product;
        return this;
    }

    public static StorefrontProduct BuildProduct(
        string id,
        int unitAmountCents = 1000,
        string currency = "USD",
        bool isAvailable = true,
        params (string variantId, int amountCents)[] variants)
    {
        var defaultPrice = new StorefrontPrice
        {
            Id = $"{id}-price",
            AmountCents = unitAmountCents,
            Currency = currency,
        };
        var productVariants = variants.Select(v => new StorefrontProductVariant
        {
            Id = v.variantId,
            Name = v.variantId,
            Price = new StorefrontPrice
            {
                Id = $"{id}-{v.variantId}-price",
                AmountCents = v.amountCents,
                Currency = currency,
            },
        }).ToArray();
        return new StorefrontProduct
        {
            Id = id,
            Slug = id,
            Name = $"Product {id}",
            DefaultPrice = defaultPrice,
            Variants = productVariants,
            IsAvailable = isAvailable,
        };
    }

    public Task<StorefrontResult<PagedResult<StorefrontProduct>>> ListProductsAsync(
        ListProductsQuery query,
        CancellationToken ct)
    {
        var rows = _products.Values.ToArray();
        return Task.FromResult(StorefrontResult<PagedResult<StorefrontProduct>>.Success(new PagedResult<StorefrontProduct>
        {
            Rows = rows,
            TotalCount = rows.Length,
            Page = query.Page,
            PageSize = query.PageSize,
        }));
    }

    public Task<StorefrontResult<StorefrontProduct>> GetProductAsync(
        string productId,
        string? language,
        CancellationToken ct)
    {
        return Task.FromResult(_products.TryGetValue(productId, out var p)
            ? StorefrontResult<StorefrontProduct>.Success(p)
            : StorefrontResult<StorefrontProduct>.Failure(new StorefrontNotFoundError(
                Message: $"Product {productId} not found.",
                CorrelationId: Guid.NewGuid().ToString("N"))));
    }

    public Task<StorefrontResult<IReadOnlyList<StorefrontCategory>>> ListCategoriesAsync(
        string? language,
        CancellationToken ct)
    {
        return Task.FromResult(StorefrontResult<IReadOnlyList<StorefrontCategory>>.Success(
            Array.Empty<StorefrontCategory>()));
    }

    public Task<StorefrontResult<StorefrontBusinessProfile>> GetBusinessProfileAsync(CancellationToken ct)
    {
        return Task.FromResult(StorefrontResult<StorefrontBusinessProfile>.Success(new StorefrontBusinessProfile
        {
            DisplayName = "Test Storefront",
        }));
    }
}
