using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Cart;

namespace PolarSharp.EcommerceStorefronts.Tests.TestSupport;

internal sealed class CartServiceFixture
{
    public FakeCatalogProvider Catalog { get; } = new();
    public InMemoryStorefrontCartStore Store { get; } = new();
    public TestStorefrontIdentityProvider Identity { get; init; } = TestStorefrontIdentityProvider.GuestSingleTenant();
    public TestGuestSessionAccessor GuestSessions { get; init; } = TestGuestSessionAccessor.WithSession(Guid.NewGuid());
    public StorefrontOptions Options { get; init; } = new();
    public FakeTimeProvider Clock { get; } = new();

    public DefaultStorefrontCartService Build() => new(
        Identity,
        GuestSessions,
        Store,
        Catalog,
        Microsoft.Extensions.Options.Options.Create(Options),
        Clock);
}
