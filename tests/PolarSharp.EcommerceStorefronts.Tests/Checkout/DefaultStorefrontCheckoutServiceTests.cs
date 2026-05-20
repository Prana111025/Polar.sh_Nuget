using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Cart;
using PolarSharp.EcommerceStorefronts.Checkout;
using PolarSharp.EcommerceStorefronts.Tests.TestSupport;

namespace PolarSharp.EcommerceStorefronts.Tests.Checkout;

public sealed class DefaultStorefrontCheckoutServiceTests
{
    [Fact]
    public async Task InitiateCheckout_succeeds_for_an_authenticated_customer_with_items()
    {
        var cartFx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.SignedInSingleTenant(Guid.NewGuid()),
            GuestSessions = TestGuestSessionAccessor.None,
        };
        cartFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p", unitAmountCents: 1000));
        var cart = cartFx.Build();
        await cart.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);

        var sessionStore = new InMemoryStorefrontCheckoutSessionStore();
        var svc = new DefaultStorefrontCheckoutService(
            cartFx.Store, sessionStore, cartFx.Identity, cartFx.GuestSessions, pipeline: null, clock: cartFx.Clock);

        var result = await svc.InitiateCheckoutAsync(new InitiateCheckoutCommand(), default);

        Assert.True(result.IsSuccess);
        var session = Unwrap(result);
        Assert.Equal(CheckoutStatus.Initiated, session.Status);
        Assert.NotNull(session.CustomerId);

        // Round-trip via GetSessionAsync.
        var got = await svc.GetSessionAsync(session.Id, default);
        Assert.True(got.IsSuccess);
        Assert.Equal(session.Id, Unwrap(got).Id);
    }

    [Fact]
    public async Task InitiateCheckout_requires_email_for_guest()
    {
        var cartFx = new CartServiceFixture();
        cartFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var cart = cartFx.Build();
        await cart.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);
        var sessionStore = new InMemoryStorefrontCheckoutSessionStore();
        var svc = new DefaultStorefrontCheckoutService(
            cartFx.Store, sessionStore, cartFx.Identity, cartFx.GuestSessions, pipeline: null, clock: cartFx.Clock);

        var result = await svc.InitiateCheckoutAsync(new InitiateCheckoutCommand(), default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontValidationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task InitiateCheckout_accepts_guest_with_email()
    {
        var cartFx = new CartServiceFixture();
        cartFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var cart = cartFx.Build();
        await cart.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);
        var sessionStore = new InMemoryStorefrontCheckoutSessionStore();
        var svc = new DefaultStorefrontCheckoutService(
            cartFx.Store, sessionStore, cartFx.Identity, cartFx.GuestSessions, pipeline: null, clock: cartFx.Clock);

        var result = await svc.InitiateCheckoutAsync(new InitiateCheckoutCommand
        {
            CustomerEmail = "guest@example.com",
        }, default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task InitiateCheckout_rejects_empty_cart()
    {
        var cartFx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.SignedInSingleTenant(Guid.NewGuid()),
            GuestSessions = TestGuestSessionAccessor.None,
        };
        // Force creation of empty cart.
        await cartFx.Build().GetCurrentCartAsync(default);
        var sessionStore = new InMemoryStorefrontCheckoutSessionStore();
        var svc = new DefaultStorefrontCheckoutService(
            cartFx.Store, sessionStore, cartFx.Identity, cartFx.GuestSessions, pipeline: null, clock: cartFx.Clock);

        var result = await svc.InitiateCheckoutAsync(new InitiateCheckoutCommand(), default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontValidationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task InitiateCheckout_returns_NotFound_when_no_cart_for_owner()
    {
        // No cart was ever added; cart store has no row.
        var cartFx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.SignedInSingleTenant(Guid.NewGuid()),
            GuestSessions = TestGuestSessionAccessor.None,
        };
        var sessionStore = new InMemoryStorefrontCheckoutSessionStore();
        var svc = new DefaultStorefrontCheckoutService(
            cartFx.Store, sessionStore, cartFx.Identity, cartFx.GuestSessions, pipeline: null, clock: cartFx.Clock);

        var result = await svc.InitiateCheckoutAsync(new InitiateCheckoutCommand(), default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontNotFoundError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task GetSession_returns_NotFound_when_session_id_unknown()
    {
        var cartFx = new CartServiceFixture();
        var svc = new DefaultStorefrontCheckoutService(
            cartFx.Store,
            new InMemoryStorefrontCheckoutSessionStore(),
            cartFx.Identity,
            cartFx.GuestSessions,
            pipeline: null,
            clock: cartFx.Clock);

        var result = await svc.GetSessionAsync(Guid.NewGuid(), default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontNotFoundError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task ProcessCheckout_yields_CheckoutFailed_when_pipeline_is_not_registered()
    {
        var cartFx = new CartServiceFixture();
        var svc = new DefaultStorefrontCheckoutService(
            cartFx.Store,
            new InMemoryStorefrontCheckoutSessionStore(),
            cartFx.Identity,
            cartFx.GuestSessions,
            pipeline: null,
            clock: cartFx.Clock);

        var events = new List<CheckoutPipelineEvent>();
        await foreach (var evt in svc.ProcessCheckoutAsync(Guid.NewGuid(), default))
        {
            events.Add(evt);
        }

        var failed = Assert.Single(events.OfType<CheckoutFailed>());
        var providerErr = Assert.IsType<StorefrontProviderError>(failed.Error);
        Assert.Equal("storefront:checkout-pipeline", providerErr.Provider);
    }

    [Fact]
    public async Task ProcessCheckout_yields_CheckoutFailed_when_session_is_unknown_and_pipeline_is_registered()
    {
        var cartFx = new CartServiceFixture();
        var pipeline = TestPipelineBuilder.Build();
        var svc = new DefaultStorefrontCheckoutService(
            cartFx.Store,
            new InMemoryStorefrontCheckoutSessionStore(),
            cartFx.Identity,
            cartFx.GuestSessions,
            pipeline,
            cartFx.Clock);

        var events = new List<CheckoutPipelineEvent>();
        await foreach (var evt in svc.ProcessCheckoutAsync(Guid.NewGuid(), default))
        {
            events.Add(evt);
        }

        var failed = Assert.Single(events.OfType<CheckoutFailed>());
        Assert.IsType<StorefrontNotFoundError>(failed.Error);
    }

    private static T Unwrap<T>(StorefrontResult<T> r) =>
        r.Match(v => v, e => throw new InvalidOperationException($"unexpected failure: {e.Message}"));
}
