using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Cart;
using PolarSharp.EcommerceStorefronts.Checkout;
using PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing;

namespace PolarSharp.EcommerceStorefronts.Tests.TestSupport;

internal sealed class CartServiceFixture
{
    public FakeCatalogProvider Catalog { get; } = new();
    public FakeTimeProvider Clock { get; } = new();
    public InMemoryStorefrontCartStore Store { get; }
    public IStorefrontIdempotencyCache Idempotency { get; }
    public TestStorefrontIdentityProvider Identity { get; init; } = TestStorefrontIdentityProvider.GuestSingleTenant();
    public TestGuestSessionAccessor GuestSessions { get; init; } = TestGuestSessionAccessor.WithSession(Guid.NewGuid());
    public StorefrontOptions Options { get; init; } = new();

    public CartServiceFixture()
    {
        Store = new InMemoryStorefrontCartStore(Clock);
        Idempotency = new InMemoryStorefrontIdempotencyCache(Clock);
    }

    public DefaultStorefrontCartService Build() => new(
        Identity,
        GuestSessions,
        Store,
        Catalog,
        Idempotency,
        Microsoft.Extensions.Options.Options.Create(Options),
        Clock);

    public DefaultStorefrontCheckoutService BuildCheckoutService(
        InMemoryStorefrontCheckoutSessionStore sessionStore,
        OrderProcessingPipeline? pipeline = null) => new(
        Store,
        sessionStore,
        Identity,
        GuestSessions,
        Idempotency,
        Microsoft.Extensions.Options.Options.Create(Options),
        pipeline,
        Clock);
}
