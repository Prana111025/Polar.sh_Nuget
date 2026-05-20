using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Tests.TestSupport;

namespace PolarSharp.EcommerceStorefronts.Tests.Cart;

public sealed class DefaultStorefrontCartServiceTests
{
    [Fact]
    public async Task GetCurrentCart_creates_an_empty_cart_for_a_guest_session()
    {
        var fx = new CartServiceFixture();
        var svc = fx.Build();

        var result = await svc.GetCurrentCartAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var cart = Unwrap(result);
        Assert.Empty(cart.LineItems);
        Assert.Null(cart.CustomerId);
        Assert.NotNull(cart.GuestSessionId);
        Assert.Null(cart.TenantId);
        Assert.Equal(0, cart.Totals.GrandTotalCents);
    }

    [Fact]
    public async Task GetCurrentCart_returns_failure_when_no_owner_can_be_resolved()
    {
        var fx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.GuestSingleTenant(),
            GuestSessions = TestGuestSessionAccessor.None,
        };
        var svc = fx.Build();

        var result = await svc.GetCurrentCartAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        var err = result.Match(_ => (StorefrontError?)null, e => e);
        Assert.IsType<StorefrontAuthenticationError>(err);
    }

    [Fact]
    public async Task AddToCart_uses_catalog_price_and_ignores_any_client_attempt_to_set_price()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p1", unitAmountCents: 2500));
        var svc = fx.Build();

        var result = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p1", Quantity = 2 }, default);

        Assert.True(result.IsSuccess);
        var cart = Unwrap(result);
        var line = Assert.Single(cart.LineItems);
        Assert.Equal(2500, line.UnitAmountCents);
        Assert.Equal(5000, line.LineSubtotalCents);
        Assert.Equal(5000, cart.Totals.GrandTotalCents);
    }

    [Fact]
    public async Task AddToCart_merges_lines_for_the_same_product_variant()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p1", unitAmountCents: 1000));
        var svc = fx.Build();

        await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p1", Quantity = 2 }, default);
        var result = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p1", Quantity = 3 }, default);

        var cart = Unwrap(result);
        var line = Assert.Single(cart.LineItems);
        Assert.Equal(5, line.Quantity);
        Assert.Equal(5000, line.LineSubtotalCents);
    }

    [Fact]
    public async Task AddToCart_rejects_zero_quantity()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p1"));
        var svc = fx.Build();

        var result = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p1", Quantity = 0 }, default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontValidationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task AddToCart_rejects_negative_quantity()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p1"));
        var svc = fx.Build();

        var result = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p1", Quantity = -5 }, default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontValidationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task AddToCart_returns_NotFound_for_unknown_product()
    {
        var fx = new CartServiceFixture();
        var svc = fx.Build();

        var result = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "missing", Quantity = 1 }, default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontNotFoundError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task AddToCart_rejects_unavailable_product()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("oos", isAvailable: false));
        var svc = fx.Build();

        var result = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "oos", Quantity = 1 }, default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontConflictError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task AddToCart_uses_variant_price_when_variantId_supplied()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct(
            "shirt",
            unitAmountCents: 2000,
            variants: new[] { ("L", 2500), ("XL", 3000) }));
        var svc = fx.Build();

        var result = await svc.AddToCartAsync(new AddToCartCommand
        {
            ProductId = "shirt",
            VariantId = "XL",
            Quantity = 2,
        }, default);

        var cart = Unwrap(result);
        var line = Assert.Single(cart.LineItems);
        Assert.Equal(3000, line.UnitAmountCents);
        Assert.Equal(6000, line.LineSubtotalCents);
    }

    [Fact]
    public async Task AddToCart_enforces_MaxCartLineItems()
    {
        var fx = new CartServiceFixture { Options = new StorefrontOptions { MaxCartLineItems = 2 } };
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("a"));
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("b"));
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("c"));
        var svc = fx.Build();

        await svc.AddToCartAsync(new AddToCartCommand { ProductId = "a", Quantity = 1 }, default);
        await svc.AddToCartAsync(new AddToCartCommand { ProductId = "b", Quantity = 1 }, default);
        var result = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "c", Quantity = 1 }, default);

        Assert.True(result.IsFailure);
        var err = result.Match(_ => (StorefrontError?)null, e => e) as StorefrontValidationError;
        Assert.NotNull(err);
        Assert.Contains("distinct lines", err!.Message);
    }

    [Fact]
    public async Task AddToCart_enforces_MaxCartTotalValueCents()
    {
        var fx = new CartServiceFixture { Options = new StorefrontOptions { MaxCartTotalValueCents = 1500 } };
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p", unitAmountCents: 1000));
        var svc = fx.Build();

        var result = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 2 }, default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontValidationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task UpdateLineQuantity_recomputes_subtotal_and_uses_authoritative_price()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p", unitAmountCents: 1000));
        var svc = fx.Build();
        var added = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);
        var lineId = Unwrap(added).LineItems[0].LineId;

        // Catalog price changes mid-life; the cart service must adopt the new price on update.
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p", unitAmountCents: 1500));

        var updated = await svc.UpdateLineQuantityAsync(new UpdateQuantityCommand { LineId = lineId, Quantity = 3 }, default);

        var cart = Unwrap(updated);
        var line = Assert.Single(cart.LineItems);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(1500, line.UnitAmountCents);
        Assert.Equal(4500, line.LineSubtotalCents);
    }

    [Fact]
    public async Task UpdateLineQuantity_removes_line_when_quantity_is_zero()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();
        var added = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);
        var lineId = Unwrap(added).LineItems[0].LineId;

        var result = await svc.UpdateLineQuantityAsync(new UpdateQuantityCommand { LineId = lineId, Quantity = 0 }, default);

        Assert.True(result.IsSuccess);
        Assert.Empty(Unwrap(result).LineItems);
    }

    [Fact]
    public async Task UpdateLineQuantity_rejects_negative()
    {
        var fx = new CartServiceFixture();
        var svc = fx.Build();
        var result = await svc.UpdateLineQuantityAsync(new UpdateQuantityCommand { LineId = "anything", Quantity = -1 }, default);
        Assert.IsType<StorefrontValidationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task UpdateLineQuantity_returns_NotFound_for_unknown_line()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();
        // Force creation of a cart first
        await svc.GetCurrentCartAsync(default);
        var result = await svc.UpdateLineQuantityAsync(new UpdateQuantityCommand { LineId = "nope", Quantity = 2 }, default);
        Assert.IsType<StorefrontNotFoundError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task RemoveLine_drops_the_line()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();
        var added = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);
        var lineId = Unwrap(added).LineItems[0].LineId;
        var removed = await svc.RemoveLineAsync(lineId, default);
        Assert.Empty(Unwrap(removed).LineItems);
    }

    [Fact]
    public async Task ApplyDiscountCode_stores_code_but_does_not_alter_subtotal()
    {
        // Server-as-source-of-truth: cart records the code, ApplyDiscountsStage validates.
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p", unitAmountCents: 1000));
        var svc = fx.Build();
        await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);

        var result = await svc.ApplyDiscountCodeAsync("SAVE10", default);

        var cart = Unwrap(result);
        Assert.Equal("SAVE10", cart.DiscountCode);
        Assert.Equal(1000, cart.Totals.GrandTotalCents);
        Assert.Equal(0, cart.Totals.DiscountCents);
    }

    [Fact]
    public async Task RemoveDiscount_clears_the_code()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();
        await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);
        await svc.ApplyDiscountCodeAsync("X", default);
        var result = await svc.RemoveDiscountAsync(default);
        Assert.Null(Unwrap(result).DiscountCode);
    }

    [Fact]
    public async Task SetShippingAddress_attaches_address_to_cart()
    {
        var fx = new CartServiceFixture();
        var svc = fx.Build();
        var addr = new ShippingAddress
        {
            FullName = "Jane Doe",
            Line1 = "123 Main",
            City = "Bend",
            PostalCode = "97701",
            CountryCode = "US",
        };
        var result = await svc.SetShippingAddressAsync(addr, default);
        Assert.True(result.IsSuccess);
        Assert.Equal("Jane Doe", Unwrap(result).ShippingAddress?.FullName);
    }

    [Fact]
    public async Task Cart_persistence_works_for_authenticated_customer_in_multi_tenant_mode()
    {
        var customerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var fx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.SignedInMultiTenant(customerId, tenantId),
            GuestSessions = TestGuestSessionAccessor.None,
        };
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();

        await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);

        // Second call should return the SAME cart (not a new one).
        var got = await svc.GetCurrentCartAsync(default);
        var cart = Unwrap(got);
        Assert.Equal(customerId, cart.CustomerId);
        Assert.Equal(tenantId, cart.TenantId);
        Assert.Single(cart.LineItems);
    }

    [Fact]
    public async Task Cart_persistence_works_for_guest_in_single_tenant_mode()
    {
        var sessionId = Guid.NewGuid();
        var fx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.GuestSingleTenant(),
            GuestSessions = TestGuestSessionAccessor.WithSession(sessionId),
        };
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();

        await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 2 }, default);

        var got = await svc.GetCurrentCartAsync(default);
        var cart = Unwrap(got);
        Assert.Equal(sessionId, cart.GuestSessionId);
        Assert.Null(cart.CustomerId);
        Assert.Null(cart.TenantId);
        Assert.Equal(2, cart.LineItems[0].Quantity);
    }

    private static Abstractions.Cart.Cart Unwrap(StorefrontResult<Abstractions.Cart.Cart> r) =>
        r.Match(c => c, e => throw new InvalidOperationException($"unexpected failure: {e.Message}"));
}
