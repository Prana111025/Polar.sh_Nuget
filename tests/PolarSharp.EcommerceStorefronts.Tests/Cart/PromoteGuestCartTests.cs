using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Tests.TestSupport;

namespace PolarSharp.EcommerceStorefronts.Tests.Cart;

public sealed class PromoteGuestCartTests
{
    [Fact]
    public async Task PromoteGuestCart_returns_AuthenticationError_when_no_authenticated_customer()
    {
        var fx = new CartServiceFixture();
        var svc = fx.Build();

        var result = await svc.PromoteGuestCartAsync(Guid.NewGuid(), default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontAuthenticationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task PromoteGuestCart_with_no_guest_cart_returns_existing_customer_cart_unchanged()
    {
        var customerId = Guid.NewGuid();
        var fx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.SignedInSingleTenant(customerId),
            GuestSessions = TestGuestSessionAccessor.None,
        };
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();
        await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 3 }, default);

        var promoted = await svc.PromoteGuestCartAsync(Guid.NewGuid(), default);

        Assert.True(promoted.IsSuccess);
        var cart = Unwrap(promoted);
        Assert.Equal(customerId, cart.CustomerId);
        Assert.Equal(3, cart.LineItems[0].Quantity);
    }

    [Fact]
    public async Task PromoteGuestCart_with_no_customer_cart_re_keys_the_guest_cart()
    {
        var customerId = Guid.NewGuid();
        var guestSessionId = Guid.NewGuid();

        // Build the guest cart first (as a guest).
        var guestFx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.GuestSingleTenant(),
            GuestSessions = TestGuestSessionAccessor.WithSession(guestSessionId),
        };
        guestFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p", unitAmountCents: 1500));
        var guestSvc = guestFx.Build();
        await guestSvc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 2 }, default);

        // Now the customer signs in. Same store + catalog + clock + idempotency, but
        // a customer-flavored identity.
        var customerSvc = new global::PolarSharp.EcommerceStorefronts.Cart.DefaultStorefrontCartService(
            TestStorefrontIdentityProvider.SignedInSingleTenant(customerId),
            TestGuestSessionAccessor.None,
            guestFx.Store,
            guestFx.Catalog,
            guestFx.Idempotency,
            Microsoft.Extensions.Options.Options.Create(guestFx.Options),
            guestFx.Clock);

        var promoted = await customerSvc.PromoteGuestCartAsync(guestSessionId, default);

        var cart = Unwrap(promoted);
        Assert.Equal(customerId, cart.CustomerId);
        Assert.Null(cart.GuestSessionId);
        Assert.Equal(2, cart.LineItems[0].Quantity);
        Assert.Equal(1500, cart.LineItems[0].UnitAmountCents);
        // Guest cart was deleted.
        var leftover = await guestFx.Store.FindByOwnerAsync(
            CartOwner.FromGuest(guestSessionId), StorefrontOption<Guid>.None, default);
        Assert.False(leftover.HasValue);
    }

    [Fact]
    public async Task PromoteGuestCart_merges_by_product_variant_and_revalidates_prices()
    {
        var customerId = Guid.NewGuid();
        var guestSessionId = Guid.NewGuid();

        var guestFx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.GuestSingleTenant(),
            GuestSessions = TestGuestSessionAccessor.WithSession(guestSessionId),
        };
        guestFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("a", unitAmountCents: 1000));
        guestFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("b", unitAmountCents: 2000));
        var guestSvc = guestFx.Build();
        await guestSvc.AddToCartAsync(new AddToCartCommand { ProductId = "a", Quantity = 2 }, default);
        await guestSvc.AddToCartAsync(new AddToCartCommand { ProductId = "b", Quantity = 1 }, default);

        // Customer already has their own cart with product A in it (so this must merge,
        // not overwrite).
        var customerSvc = new global::PolarSharp.EcommerceStorefronts.Cart.DefaultStorefrontCartService(
            TestStorefrontIdentityProvider.SignedInSingleTenant(customerId),
            TestGuestSessionAccessor.None,
            guestFx.Store,
            guestFx.Catalog,
            guestFx.Idempotency,
            Microsoft.Extensions.Options.Options.Create(guestFx.Options),
            guestFx.Clock);
        await customerSvc.AddToCartAsync(new AddToCartCommand { ProductId = "a", Quantity = 1 }, default);

        // Catalog raises product A's price BEFORE the promote — the re-validate must
        // pick up the new price.
        guestFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("a", unitAmountCents: 1100));

        var promoted = await customerSvc.PromoteGuestCartAsync(guestSessionId, default);

        var cart = Unwrap(promoted);
        Assert.Equal(customerId, cart.CustomerId);
        Assert.Equal(2, cart.LineItems.Count);
        var lineA = cart.LineItems.Single(li => li.ProductId == "a");
        var lineB = cart.LineItems.Single(li => li.ProductId == "b");
        Assert.Equal(3, lineA.Quantity);            // 1 customer + 2 guest = 3
        Assert.Equal(1100, lineA.UnitAmountCents);  // catalog-current price wins
        Assert.Equal(1, lineB.Quantity);
        Assert.Equal(2000, lineB.UnitAmountCents);

        // Guest cart deleted.
        var leftover = await guestFx.Store.FindByOwnerAsync(
            CartOwner.FromGuest(guestSessionId), StorefrontOption<Guid>.None, default);
        Assert.False(leftover.HasValue);
    }

    [Fact]
    public async Task PromoteGuestCart_silently_drops_unavailable_lines_on_merge()
    {
        var customerId = Guid.NewGuid();
        var guestSessionId = Guid.NewGuid();

        var guestFx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.GuestSingleTenant(),
            GuestSessions = TestGuestSessionAccessor.WithSession(guestSessionId),
        };
        guestFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("a", unitAmountCents: 1000));
        guestFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("b", unitAmountCents: 2000));
        var guestSvc = guestFx.Build();
        await guestSvc.AddToCartAsync(new AddToCartCommand { ProductId = "a", Quantity = 1 }, default);
        await guestSvc.AddToCartAsync(new AddToCartCommand { ProductId = "b", Quantity = 1 }, default);

        // Product B becomes unavailable BEFORE the promote — the merged cart should
        // omit it rather than failing the entire promote.
        guestFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("b", unitAmountCents: 2000, isAvailable: false));

        var customerSvc = new global::PolarSharp.EcommerceStorefronts.Cart.DefaultStorefrontCartService(
            TestStorefrontIdentityProvider.SignedInSingleTenant(customerId),
            TestGuestSessionAccessor.None,
            guestFx.Store,
            guestFx.Catalog,
            guestFx.Idempotency,
            Microsoft.Extensions.Options.Options.Create(guestFx.Options),
            guestFx.Clock);

        var promoted = await customerSvc.PromoteGuestCartAsync(guestSessionId, default);

        var cart = Unwrap(promoted);
        Assert.Single(cart.LineItems);
        Assert.Equal("a", cart.LineItems[0].ProductId);
    }

    [Fact]
    public async Task PromoteGuestCart_with_empty_guest_cart_returns_customer_cart()
    {
        var customerId = Guid.NewGuid();
        var guestSessionId = Guid.NewGuid();

        var guestFx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.GuestSingleTenant(),
            GuestSessions = TestGuestSessionAccessor.WithSession(guestSessionId),
        };
        var guestSvc = guestFx.Build();
        // Force creation of the empty guest cart.
        await guestSvc.GetCurrentCartAsync(default);

        var customerSvc = new global::PolarSharp.EcommerceStorefronts.Cart.DefaultStorefrontCartService(
            TestStorefrontIdentityProvider.SignedInSingleTenant(customerId),
            TestGuestSessionAccessor.None,
            guestFx.Store,
            guestFx.Catalog,
            guestFx.Idempotency,
            Microsoft.Extensions.Options.Options.Create(guestFx.Options),
            guestFx.Clock);

        var promoted = await customerSvc.PromoteGuestCartAsync(guestSessionId, default);

        Assert.True(promoted.IsSuccess);
        var cart = Unwrap(promoted);
        Assert.Equal(customerId, cart.CustomerId);
        Assert.Empty(cart.LineItems);
    }

    private static Abstractions.Cart.Cart Unwrap(StorefrontResult<Abstractions.Cart.Cart> r) =>
        r.Match(c => c, e => throw new InvalidOperationException($"unexpected failure: {e.Message}"));
}
