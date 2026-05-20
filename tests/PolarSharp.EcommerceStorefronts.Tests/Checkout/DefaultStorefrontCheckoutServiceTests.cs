using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
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

        var svc = cartFx.BuildCheckoutService(new InMemoryStorefrontCheckoutSessionStore());

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
        var svc = cartFx.BuildCheckoutService(new InMemoryStorefrontCheckoutSessionStore());

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
        var svc = cartFx.BuildCheckoutService(new InMemoryStorefrontCheckoutSessionStore());

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
        var svc = cartFx.BuildCheckoutService(new InMemoryStorefrontCheckoutSessionStore());

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
        var svc = cartFx.BuildCheckoutService(new InMemoryStorefrontCheckoutSessionStore());

        var result = await svc.InitiateCheckoutAsync(new InitiateCheckoutCommand(), default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontNotFoundError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task GetSession_returns_NotFound_when_session_id_unknown()
    {
        var cartFx = new CartServiceFixture();
        var svc = cartFx.BuildCheckoutService(new InMemoryStorefrontCheckoutSessionStore());

        var result = await svc.GetSessionAsync(Guid.NewGuid(), default);

        Assert.True(result.IsFailure);
        Assert.IsType<StorefrontNotFoundError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task ProcessCheckout_yields_CheckoutFailed_when_pipeline_is_not_registered()
    {
        var cartFx = new CartServiceFixture();
        var svc = cartFx.BuildCheckoutService(new InMemoryStorefrontCheckoutSessionStore());

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
        var svc = cartFx.BuildCheckoutService(new InMemoryStorefrontCheckoutSessionStore(), pipeline);

        var events = new List<CheckoutPipelineEvent>();
        await foreach (var evt in svc.ProcessCheckoutAsync(Guid.NewGuid(), default))
        {
            events.Add(evt);
        }

        var failed = Assert.Single(events.OfType<CheckoutFailed>());
        Assert.IsType<StorefrontNotFoundError>(failed.Error);
    }

    [Fact]
    public async Task InitiateCheckout_with_idempotency_token_short_circuits_on_replay()
    {
        var cartFx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.SignedInSingleTenant(Guid.NewGuid()),
            GuestSessions = TestGuestSessionAccessor.None,
        };
        cartFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p", unitAmountCents: 1000));
        var cart = cartFx.Build();
        await cart.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);
        var svc = cartFx.BuildCheckoutService(new InMemoryStorefrontCheckoutSessionStore());

        var first = await svc.InitiateCheckoutAsync(new InitiateCheckoutCommand
        {
            IdempotencyToken = "tok-1",
        }, default);
        var second = await svc.InitiateCheckoutAsync(new InitiateCheckoutCommand
        {
            IdempotencyToken = "tok-1",
        }, default);

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(Unwrap(first).Id, Unwrap(second).Id);
    }

    private static T Unwrap<T>(StorefrontResult<T> r) =>
        r.Match(v => v, e => throw new InvalidOperationException($"unexpected failure: {e.Message}"));
}
