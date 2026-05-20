using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Tests.TestSupport;

namespace PolarSharp.EcommerceStorefronts.Tests.Cart;

public sealed class CartIdempotencyAndExpiryTests
{
    [Fact]
    public async Task AddToCart_with_idempotency_token_replays_first_response_on_retry()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p", unitAmountCents: 1000));
        var svc = fx.Build();

        var first = await svc.AddToCartAsync(new AddToCartCommand
        {
            ProductId = "p",
            Quantity = 2,
            IdempotencyToken = "abc123",
        }, default);
        // A retry with the SAME token must short-circuit — even though it would normally
        // append another 2 units, the cached response wins.
        var replay = await svc.AddToCartAsync(new AddToCartCommand
        {
            ProductId = "p",
            Quantity = 2,
            IdempotencyToken = "abc123",
        }, default);

        var firstCart = Unwrap(first);
        var replayCart = Unwrap(replay);
        Assert.Equal(firstCart.Id, replayCart.Id);
        Assert.Equal(2, replayCart.LineItems[0].Quantity);
        // And the stored cart should reflect the SAME state — the replay didn't bump it.
        var loaded = await svc.GetCurrentCartAsync(default);
        Assert.Equal(2, Unwrap(loaded).LineItems[0].Quantity);
    }

    [Fact]
    public async Task AddToCart_with_no_idempotency_token_does_NOT_dedupe()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();

        await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);
        await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);

        var loaded = await svc.GetCurrentCartAsync(default);
        Assert.Equal(2, Unwrap(loaded).LineItems[0].Quantity);
    }

    [Fact]
    public async Task UpdateLineQuantity_honours_idempotency_token()
    {
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();
        var added = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);
        var lineId = Unwrap(added).LineItems[0].LineId;

        var first = await svc.UpdateLineQuantityAsync(new UpdateQuantityCommand
        {
            LineId = lineId,
            Quantity = 5,
            IdempotencyToken = "upd-1",
        }, default);
        // Replay with the SAME token must return the same response. Even though the
        // command says quantity=10 below, the cache replays the first response (5).
        var replay = await svc.UpdateLineQuantityAsync(new UpdateQuantityCommand
        {
            LineId = lineId,
            Quantity = 10,
            IdempotencyToken = "upd-1",
        }, default);

        Assert.Equal(5, Unwrap(first).LineItems[0].Quantity);
        Assert.Equal(5, Unwrap(replay).LineItems[0].Quantity);
    }

    [Fact]
    public async Task Idempotency_keys_are_scoped_to_command_kind()
    {
        // Same token reused across AddToCart + UpdateQuantity must NOT collide.
        var fx = new CartServiceFixture();
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();

        var added = await svc.AddToCartAsync(new AddToCartCommand
        {
            ProductId = "p",
            Quantity = 1,
            IdempotencyToken = "shared",
        }, default);
        var lineId = Unwrap(added).LineItems[0].LineId;

        var updated = await svc.UpdateLineQuantityAsync(new UpdateQuantityCommand
        {
            LineId = lineId,
            Quantity = 7,
            IdempotencyToken = "shared", // same token, different command kind — must NOT replay AddToCart
        }, default);

        Assert.Equal(7, Unwrap(updated).LineItems[0].Quantity);
    }

    [Fact]
    public async Task Idempotency_keys_partition_by_owner()
    {
        // Two different guest sessions reusing the same idempotency token must NOT
        // see each other's cached responses.
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        var fxA = new CartServiceFixture
        {
            GuestSessions = TestGuestSessionAccessor.WithSession(sessionA),
        };
        fxA.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svcA = fxA.Build();
        var fxB = new CartServiceFixture
        {
            GuestSessions = TestGuestSessionAccessor.WithSession(sessionB),
        };
        fxB.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        // Share the SAME idempotency cache to simulate a single process.
        var svcB = new global::PolarSharp.EcommerceStorefronts.Cart.DefaultStorefrontCartService(
            fxB.Identity,
            fxB.GuestSessions,
            fxB.Store,
            fxB.Catalog,
            fxA.Idempotency, // shared
            Microsoft.Extensions.Options.Options.Create(fxB.Options),
            fxB.Clock);

        await svcA.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1, IdempotencyToken = "tok" }, default);
        var bResult = await svcB.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1, IdempotencyToken = "tok" }, default);

        Assert.True(bResult.IsSuccess);
        Assert.Equal(1, Unwrap(bResult).LineItems[0].Quantity);
        Assert.Equal(sessionB, Unwrap(bResult).GuestSessionId);
    }

    [Fact]
    public async Task Cart_save_stamps_ExpiresAt_from_options()
    {
        var fx = new CartServiceFixture { Options = new StorefrontOptions { CartLifetime = TimeSpan.FromHours(2) } };
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();

        var result = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);

        var cart = Unwrap(result);
        Assert.NotNull(cart.ExpiresAt);
        Assert.Equal(fx.Clock.GetUtcNow() + TimeSpan.FromHours(2), cart.ExpiresAt!.Value);
    }

    [Fact]
    public async Task Expired_cart_is_treated_as_absent_on_subsequent_load()
    {
        var fx = new CartServiceFixture { Options = new StorefrontOptions { CartLifetime = TimeSpan.FromMinutes(10) } };
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();

        var first = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 3 }, default);
        var firstCartId = Unwrap(first).Id;

        // Advance past the lifetime — the cart should be treated as absent and a fresh
        // empty one should be created on the next load.
        fx.Clock.Advance(TimeSpan.FromMinutes(15));

        var loaded = await svc.GetCurrentCartAsync(default);
        var freshCart = Unwrap(loaded);
        Assert.NotEqual(firstCartId, freshCart.Id);
        Assert.Empty(freshCart.LineItems);
    }

    [Fact]
    public async Task Active_cart_just_under_lifetime_is_still_returned()
    {
        var fx = new CartServiceFixture { Options = new StorefrontOptions { CartLifetime = TimeSpan.FromMinutes(10) } };
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();

        var first = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 3 }, default);
        var cartId = Unwrap(first).Id;

        fx.Clock.Advance(TimeSpan.FromMinutes(5));
        var loaded = await svc.GetCurrentCartAsync(default);
        Assert.Equal(cartId, Unwrap(loaded).Id);
    }

    [Fact]
    public async Task Mutating_an_active_cart_refreshes_ExpiresAt()
    {
        var fx = new CartServiceFixture { Options = new StorefrontOptions { CartLifetime = TimeSpan.FromMinutes(10) } };
        fx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p"));
        var svc = fx.Build();

        await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);
        fx.Clock.Advance(TimeSpan.FromMinutes(5));
        var updated = await svc.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 2 }, default);
        var cart = Unwrap(updated);
        Assert.Equal(fx.Clock.GetUtcNow() + TimeSpan.FromMinutes(10), cart.ExpiresAt!.Value);
    }

    private static Abstractions.Cart.Cart Unwrap(StorefrontResult<Abstractions.Cart.Cart> r) =>
        r.Match(c => c, e => throw new InvalidOperationException($"unexpected failure: {e.Message}"));
}
