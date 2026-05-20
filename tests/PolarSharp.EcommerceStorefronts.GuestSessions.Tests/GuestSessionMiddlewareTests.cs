using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStorefronts;
using PolarSharp.EcommerceStorefronts.GuestSessions;

namespace PolarSharp.EcommerceStorefronts.GuestSessions.Tests;

public sealed class GuestSessionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_creates_session_when_no_cookie_present_and_attaches_to_items()
    {
        var (svc, _) = BuildService();
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new GuestSessionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, svc);

        Assert.True(nextCalled);
        var stored = Assert.IsType<GuestSession>(context.Items[GuestSessionMiddleware.HttpContextItemKey]);
        Assert.NotEqual(Guid.Empty, stored.Id);
    }

    [Fact]
    public async Task InvokeAsync_reuses_existing_session_when_cookie_present()
    {
        var (svc, _) = BuildService();
        var seedContext = new DefaultHttpContext();
        var created = svc.Create(seedContext);

        var nextContext = new DefaultHttpContext();
        foreach (var setCookie in seedContext.Response.Headers.SetCookie)
        {
            if (setCookie is null) continue;
            var semi = setCookie.IndexOf(';');
            var nv = semi >= 0 ? setCookie[..semi] : setCookie;
            nextContext.Request.Headers.Append("Cookie", nv);
        }
        var middleware = new GuestSessionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(nextContext, svc);

        var stored = (GuestSession?)nextContext.Items[GuestSessionMiddleware.HttpContextItemKey];
        Assert.NotNull(stored);
        Assert.Equal(created.Id, stored!.Id);
    }

    [Fact]
    public void HttpContextGuestSessionAccessor_returns_session_id_from_items()
    {
        var accessor = new HttpContextAccessor();
        var context = new DefaultHttpContext();
        var session = new GuestSession
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        };
        context.Items[GuestSessionMiddleware.HttpContextItemKey] = session;
        accessor.HttpContext = context;

        var subject = new HttpContextGuestSessionAccessor(accessor);

        Assert.True(subject.CurrentGuestSessionId.HasValue);
        Assert.Equal(session.Id, subject.CurrentGuestSessionId.GetValueOrDefault(Guid.Empty));
    }

    [Fact]
    public void HttpContextGuestSessionAccessor_returns_None_when_no_context()
    {
        var accessor = new HttpContextAccessor();
        var subject = new HttpContextGuestSessionAccessor(accessor);
        Assert.False(subject.CurrentGuestSessionId.HasValue);
    }

    [Fact]
    public void HttpContextGuestSessionAccessor_returns_None_when_no_session_on_items()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var subject = new HttpContextGuestSessionAccessor(accessor);
        Assert.False(subject.CurrentGuestSessionId.HasValue);
    }

    private static (SignedCookieGuestSessionService svc, IDataProtectionProvider provider) BuildService()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<IDataProtectionProvider>();
        return (new SignedCookieGuestSessionService(provider, Options.Create(new StorefrontOptions())), provider);
    }
}
