using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Catalog;
using PolarSharp.EcommerceStorefronts.Abstractions.Checkout;
using PolarSharp.EcommerceStorefronts.Abstractions.Customers;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;
using PolarSharp.EcommerceStorefronts.Extensions;
using PolarSharp.EcommerceStorefronts.Tests.TestSupport;

namespace PolarSharp.EcommerceStorefronts.Tests.Composition;

public sealed class StorefrontServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPolarStorefrontsCore_resolves_every_storefront_service()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStorefrontCatalogProvider>(new FakeCatalogProvider());
        services.AddPolarStorefrontsCore();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IStorefrontIdentityProvider>());
        Assert.NotNull(scope.ServiceProvider.GetService<IGuestSessionAccessor>());
        Assert.NotNull(scope.ServiceProvider.GetService<IStorefrontCartService>());
        Assert.NotNull(scope.ServiceProvider.GetService<IStorefrontCheckoutService>());
        Assert.NotNull(scope.ServiceProvider.GetService<IStorefrontCustomerService>());
        Assert.NotNull(scope.ServiceProvider.GetService<IStorefrontCartStore>());
        Assert.NotNull(scope.ServiceProvider.GetService<IStorefrontCheckoutSessionStore>());
        Assert.NotNull(scope.ServiceProvider.GetService<IStorefrontCustomerSource>());
        Assert.NotNull(scope.ServiceProvider.GetService<IStorefrontClient>());
    }

    [Fact]
    public void AddPolarStorefrontsCore_honours_StorefrontOptions_callback()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStorefrontCatalogProvider>(new FakeCatalogProvider());
        services.AddPolarStorefrontsCore(opts =>
        {
            opts.MaxCartLineItems = 7;
            opts.GuestSessionCookieName = "custom_cookie";
        });

        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorefrontOptions>>().Value;
        Assert.Equal(7, opts.MaxCartLineItems);
        Assert.Equal("custom_cookie", opts.GuestSessionCookieName);
    }

    [Fact]
    public void IStorefrontClient_exposes_all_four_services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStorefrontCatalogProvider>(new FakeCatalogProvider());
        services.AddPolarStorefrontsCore();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IStorefrontClient>();

        Assert.NotNull(client.Catalog);
        Assert.NotNull(client.Cart);
        Assert.NotNull(client.Checkout);
        Assert.NotNull(client.Customer);
    }

    [Fact]
    public void Host_pre_registration_of_identity_provider_overrides_anonymous_default()
    {
        var services = new ServiceCollection();
        var customerId = Guid.NewGuid();
        services.AddScoped<IStorefrontIdentityProvider>(_ =>
            TestStorefrontIdentityProvider.SignedInSingleTenant(customerId));
        services.AddSingleton<IStorefrontCatalogProvider>(new FakeCatalogProvider());
        services.AddPolarStorefrontsCore();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IStorefrontIdentityProvider>();

        Assert.True(identity.IsAuthenticated);
        Assert.Equal(customerId, identity.CurrentCustomerId.GetValueOrDefault(Guid.Empty));
    }
}
