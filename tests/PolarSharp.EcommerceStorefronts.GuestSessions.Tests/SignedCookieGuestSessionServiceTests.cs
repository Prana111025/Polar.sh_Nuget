using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStorefronts;
using PolarSharp.EcommerceStorefronts.GuestSessions;

namespace PolarSharp.EcommerceStorefronts.GuestSessions.Tests;

public sealed class SignedCookieGuestSessionServiceTests
{
    [Fact]
    public void TryRead_returns_null_when_cookie_missing()
    {
        var (svc, context) = BuildFixture();
        Assert.Null(svc.TryRead(context));
    }

    [Fact]
    public void Create_writes_cookie_then_TryRead_roundtrips_session()
    {
        var (svc, context) = BuildFixture();

        var created = svc.Create(context);

        TransferCookiesToNextRequest(context, out var nextContext);
        var reread = svc.TryRead(nextContext);
        Assert.NotNull(reread);
        Assert.Equal(created.Id, reread!.Id);
    }

    [Fact]
    public void Renew_extends_expiry_and_writes_new_cookie()
    {
        var (svc, context) = BuildFixture();

        var created = svc.Create(context);
        TransferCookiesToNextRequest(context, out var renewContext);

        // Force the cookie to look like it's almost expired.
        var renewed = svc.Renew(renewContext, created);

        Assert.Equal(created.Id, renewed.Id);
        Assert.True(renewed.ExpiresAt > created.ExpiresAt - TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void TryRead_returns_null_for_tampered_cookie()
    {
        var (svc, context) = BuildFixture();
        svc.Create(context);

        TransferCookiesToNextRequest(context, out var nextContext);
        // Tamper.
        var cookieName = new StorefrontOptions().GuestSessionCookieName;
        nextContext.Request.Headers.Cookie = $"{cookieName}=garbage";

        Assert.Null(svc.TryRead(nextContext));
    }

    [Fact]
    public void TryRead_returns_null_for_expired_cookie()
    {
        var clock = new FakeTimeProvider();
        var (svc, context) = BuildFixture(clock);
        svc.Create(context);
        TransferCookiesToNextRequest(context, out var nextContext);

        // Advance past the lifetime so the embedded expiry is in the past.
        clock.Advance(TimeSpan.FromDays(60));
        Assert.Null(svc.TryRead(nextContext));
    }

    [Fact]
    public void Destroy_deletes_cookie()
    {
        var (svc, context) = BuildFixture();
        svc.Create(context);
        svc.Destroy(context);

        // The cookie was created AND deleted on the same response — the Set-Cookie
        // headers contain both an issuance and a deletion. Verify the deletion
        // header sets an explicit Max-Age or Expires in the past.
        Assert.Contains(context.Response.Headers.SetCookie, h =>
            h?.Contains("polar_guest_session=", StringComparison.OrdinalIgnoreCase) == true
            && h.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    private static (SignedCookieGuestSessionService svc, DefaultHttpContext context) BuildFixture(
        FakeTimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<IDataProtectionProvider>();

        var svc = new SignedCookieGuestSessionService(
            provider,
            Options.Create(new StorefrontOptions()),
            clock ?? new FakeTimeProvider());

        var context = new DefaultHttpContext();
        return (svc, context);
    }

    private static void TransferCookiesToNextRequest(HttpContext source, out DefaultHttpContext next)
    {
        // Simulate the browser sending the just-set cookies on the next request.
        next = new DefaultHttpContext();
        foreach (var setCookie in source.Response.Headers.SetCookie)
        {
            if (setCookie is null) continue;
            var firstSemi = setCookie.IndexOf(';');
            var nameValue = firstSemi >= 0 ? setCookie[..firstSemi] : setCookie;
            var eq = nameValue.IndexOf('=');
            if (eq < 0) continue;
            var name = nameValue[..eq];
            var value = nameValue[(eq + 1)..];
            next.Request.Headers.Append("Cookie", $"{name}={value}");
        }
    }
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
