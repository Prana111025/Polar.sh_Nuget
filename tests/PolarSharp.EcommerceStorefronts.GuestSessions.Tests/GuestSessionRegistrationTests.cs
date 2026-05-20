using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;
using PolarSharp.EcommerceStorefronts.Extensions;
using PolarSharp.EcommerceStorefronts.GuestSessions.Extensions;

namespace PolarSharp.EcommerceStorefronts.GuestSessions.Tests;

public sealed class GuestSessionRegistrationTests
{
    [Fact]
    public void AddPolarGuestSessions_registers_signed_cookie_service_and_http_accessor()
    {
        var services = new ServiceCollection();
        services.AddPolarGuestSessions();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IGuestSessionService>());
        Assert.NotNull(scope.ServiceProvider.GetService<IHttpContextAccessor>());
        var accessor = scope.ServiceProvider.GetService<IGuestSessionAccessor>();
        Assert.NotNull(accessor);
        Assert.IsType<HttpContextGuestSessionAccessor>(accessor);
    }

    [Fact]
    public void AddPolarGuestSessions_overrides_storefront_core_null_accessor()
    {
        // Wire core first, then guest sessions; the Replace() call inside
        // AddPolarGuestSessions should win.
        var services = new ServiceCollection();
        services.AddPolarStorefrontsCore();
        services.AddPolarGuestSessions();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<IGuestSessionAccessor>();
        Assert.IsType<HttpContextGuestSessionAccessor>(accessor);
    }

    [Fact]
    public void UsePolarGuestSessions_adds_middleware_to_pipeline()
    {
        // Cheap smoke test — just verifies the extension wires the type without error.
        var services = new ServiceCollection();
        services.AddPolarGuestSessions();
        var appServices = services.BuildServiceProvider();
        var app = new ApplicationBuilder(appServices);
        var result = app.UsePolarGuestSessions();
        Assert.Same(app, result);
    }
}
