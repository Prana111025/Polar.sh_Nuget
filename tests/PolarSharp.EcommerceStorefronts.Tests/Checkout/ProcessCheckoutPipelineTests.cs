using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Cart;
using PolarSharp.EcommerceStorefronts.Checkout;
using PolarSharp.EcommerceStorefronts.Tests.TestSupport;

namespace PolarSharp.EcommerceStorefronts.Tests.Checkout;

public sealed class ProcessCheckoutPipelineTests
{
    [Fact]
    public async Task ProcessCheckout_yields_CheckoutSucceeded_when_pipeline_drives_to_Completed()
    {
        var (svc, sessionId, _) = await BuildAuthenticatedScenario(new CompletingStage());

        var events = new List<CheckoutPipelineEvent>();
        await foreach (var evt in svc.ProcessCheckoutAsync(sessionId, default))
        {
            events.Add(evt);
        }

        Assert.Contains(events, e => e is CheckoutStageStarted);
        var succeeded = Assert.Single(events.OfType<CheckoutSucceeded>());
        Assert.False(string.IsNullOrEmpty(succeeded.OrderId));
    }

    [Fact]
    public async Task ProcessCheckout_yields_CheckoutFailed_when_pipeline_stage_fails()
    {
        var (svc, sessionId, _) = await BuildAuthenticatedScenario(new FailingStage());

        var events = new List<CheckoutPipelineEvent>();
        await foreach (var evt in svc.ProcessCheckoutAsync(sessionId, default))
        {
            events.Add(evt);
        }

        var failed = Assert.Single(events.OfType<CheckoutFailed>());
        Assert.Equal(CheckoutStatus.PaymentCaptured, failed.Stage);
    }

    [Fact]
    public async Task ProcessCheckout_persists_terminal_session_status()
    {
        var (svc, sessionId, sessionStore) = await BuildAuthenticatedScenario(new CompletingStage());

        await foreach (var _ in svc.ProcessCheckoutAsync(sessionId, default)) { /* drain */ }

        var loaded = await sessionStore.FindByIdAsync(sessionId, default);
        Assert.True(loaded.HasValue);
        var session = loaded.GetValueOrDefault(default!);
        Assert.Equal(CheckoutStatus.Completed, session.Status);
        Assert.NotNull(session.OrderId);
        Assert.NotNull(session.CompletedAt);
    }

    private static async Task<(DefaultStorefrontCheckoutService svc, Guid sessionId, InMemoryStorefrontCheckoutSessionStore store)>
        BuildAuthenticatedScenario(Abstractions.Pipelines.IOrderProcessingStage stage)
    {
        var customerId = Guid.NewGuid();
        var cartFx = new CartServiceFixture
        {
            Identity = TestStorefrontIdentityProvider.SignedInSingleTenant(customerId),
            GuestSessions = TestGuestSessionAccessor.None,
        };
        cartFx.Catalog.WithProduct(FakeCatalogProvider.BuildProduct("p", unitAmountCents: 1000));
        var cart = cartFx.Build();
        await cart.AddToCartAsync(new AddToCartCommand { ProductId = "p", Quantity = 1 }, default);

        var sessionStore = new InMemoryStorefrontCheckoutSessionStore();
        var pipeline = TestPipelineBuilder.Build(stage);
        var svc = cartFx.BuildCheckoutService(sessionStore, pipeline);
        var session = (await svc.InitiateCheckoutAsync(new InitiateCheckoutCommand(), default))
            .Match(s => s, e => throw new InvalidOperationException(e.Message));
        return (svc, session.Id, sessionStore);
    }
}
