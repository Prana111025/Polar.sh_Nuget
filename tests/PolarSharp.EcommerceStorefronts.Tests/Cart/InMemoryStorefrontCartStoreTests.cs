using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Cart;

namespace PolarSharp.EcommerceStorefronts.Tests.Cart;

public sealed class InMemoryStorefrontCartStoreTests
{
    [Fact]
    public async Task FindByOwner_returns_None_when_empty()
    {
        var store = new InMemoryStorefrontCartStore();
        var found = await store.FindByOwnerAsync(CartOwner.FromCustomer(Guid.NewGuid()), StorefrontOption<Guid>.None, default);
        Assert.False(found.HasValue);
    }

    [Fact]
    public async Task Save_then_FindByOwner_roundtrips_the_cart()
    {
        var store = new InMemoryStorefrontCartStore();
        var customerId = Guid.NewGuid();
        var cart = MakeCart(customerId: customerId);
        await store.SaveAsync(cart, default);

        var found = await store.FindByOwnerAsync(CartOwner.FromCustomer(customerId), StorefrontOption<Guid>.None, default);
        Assert.True(found.HasValue);
        Assert.Equal(cart.Id, found.GetValueOrDefault(default!).Id);
    }

    [Fact]
    public async Task FindById_returns_cart_after_save()
    {
        var store = new InMemoryStorefrontCartStore();
        var cart = MakeCart(customerId: Guid.NewGuid());
        await store.SaveAsync(cart, default);
        var found = await store.FindByIdAsync(cart.Id, default);
        Assert.True(found.HasValue);
    }

    [Fact]
    public async Task DeleteByOwner_removes_the_cart()
    {
        var store = new InMemoryStorefrontCartStore();
        var customerId = Guid.NewGuid();
        var cart = MakeCart(customerId: customerId);
        await store.SaveAsync(cart, default);
        await store.DeleteByOwnerAsync(CartOwner.FromCustomer(customerId), StorefrontOption<Guid>.None, default);

        var found = await store.FindByOwnerAsync(CartOwner.FromCustomer(customerId), StorefrontOption<Guid>.None, default);
        Assert.False(found.HasValue);
        var byId = await store.FindByIdAsync(cart.Id, default);
        Assert.False(byId.HasValue);
    }

    [Fact]
    public async Task Multi_tenant_carts_are_partitioned_by_tenant_id()
    {
        var store = new InMemoryStorefrontCartStore();
        var customerId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var cartA = MakeCart(customerId: customerId, tenantId: tenantA);
        var cartB = MakeCart(customerId: customerId, tenantId: tenantB);
        await store.SaveAsync(cartA, default);
        await store.SaveAsync(cartB, default);

        var foundA = await store.FindByOwnerAsync(CartOwner.FromCustomer(customerId), StorefrontOption<Guid>.Some(tenantA), default);
        var foundB = await store.FindByOwnerAsync(CartOwner.FromCustomer(customerId), StorefrontOption<Guid>.Some(tenantB), default);
        Assert.True(foundA.HasValue && foundB.HasValue);
        Assert.NotEqual(foundA.GetValueOrDefault(default!).Id, foundB.GetValueOrDefault(default!).Id);
    }

    [Fact]
    public async Task SaveAsync_throws_on_cart_with_neither_customer_nor_guest_id()
    {
        var store = new InMemoryStorefrontCartStore();
        var cart = MakeCart(); // both nulls
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(cart, default));
    }

    private static Abstractions.Cart.Cart MakeCart(Guid? customerId = null, Guid? guestSessionId = null, Guid? tenantId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            GuestSessionId = guestSessionId,
            TenantId = tenantId,
            LineItems = Array.Empty<CartLineItem>(),
            Totals = new CartTotals
            {
                SubtotalCents = 0,
                GrandTotalCents = 0,
                Currency = "USD",
            },
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
