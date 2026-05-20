using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStorefronts;

namespace PolarSharp.EcommerceStorefronts.GuestSessions;

/// <summary>
/// Default <see cref="IGuestSessionService"/> backed by ASP.NET Core data-protection
/// signing keys. Reads + writes a cookie whose payload is a compact pipe-delimited
/// triple (id|createdAtTicks|expiresAtTicks) sealed by <see cref="IDataProtector.Protect(byte[])"/>.
/// </summary>
/// <remarks>
/// The payload format is intentionally minimal: storefront-core never carries
/// PII on the guest session cookie, only the session identifier + creation +
/// expiry timestamps. Anything richer (UTM tags, last-page-viewed) belongs on the
/// server side keyed by the session id.
/// <para>
/// Protector purpose chain is versioned (<c>v1</c>) so a future format change can ship
/// as <c>v2</c> alongside <c>v1</c> reads, allowing seamless rollover.
/// </para>
/// </remarks>
public sealed class SignedCookieGuestSessionService : IGuestSessionService
{
    private const string ProtectorPurpose = "PolarSharp.EcommerceStorefronts.GuestSessions.v1";
    private const char PayloadSeparator = '|';

    private readonly IDataProtector _protector;
    private readonly StorefrontOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>Constructs the service over the supplied protector + options.</summary>
    /// <param name="protectionProvider">Source of signing keys.</param>
    /// <param name="options">Storefront tunables — cookie name, lifetime.</param>
    /// <param name="clock">Clock used for creation + expiry timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="protectionProvider"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public SignedCookieGuestSessionService(
        IDataProtectionProvider protectionProvider,
        IOptions<StorefrontOptions> options,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(protectionProvider);
        ArgumentNullException.ThrowIfNull(options);
        _protector = protectionProvider.CreateProtector(ProtectorPurpose);
        _options = options.Value;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public GuestSession? TryRead(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Cookies.TryGetValue(_options.GuestSessionCookieName, out var cookie)
            || string.IsNullOrWhiteSpace(cookie))
        {
            return null;
        }

        byte[] payload;
        try
        {
            payload = _protector.Unprotect(WebSafeFromBase64(cookie));
        }
        catch
        {
            // Tampered / expired keys / format change — treat as "no session" so the
            // middleware will mint a fresh one. We deliberately do NOT throw here.
            return null;
        }

        var parts = Encoding.UTF8.GetString(payload).Split(PayloadSeparator);
        if (parts.Length != 3
            || !Guid.TryParseExact(parts[0], "N", out var id)
            || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var createdTicks)
            || !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresTicks))
        {
            return null;
        }

        var expiresAt = new DateTimeOffset(expiresTicks, TimeSpan.Zero);
        if (expiresAt <= _clock.GetUtcNow())
        {
            return null;
        }

        return new GuestSession
        {
            Id = id,
            CreatedAt = new DateTimeOffset(createdTicks, TimeSpan.Zero),
            ExpiresAt = expiresAt,
        };
    }

    /// <inheritdoc/>
    public GuestSession Create(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var now = _clock.GetUtcNow();
        var session = new GuestSession
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            ExpiresAt = now + _options.GuestSessionLifetime,
        };
        WriteCookie(context, session);
        return session;
    }

    /// <inheritdoc/>
    public GuestSession Renew(HttpContext context, GuestSession session)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(session);
        var renewed = session with { ExpiresAt = _clock.GetUtcNow() + _options.GuestSessionLifetime };
        WriteCookie(context, renewed);
        return renewed;
    }

    /// <inheritdoc/>
    public void Destroy(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.Cookies.Delete(_options.GuestSessionCookieName);
    }

    private void WriteCookie(HttpContext context, GuestSession session)
    {
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{session.Id:N}{PayloadSeparator}{session.CreatedAt.UtcTicks}{PayloadSeparator}{session.ExpiresAt.UtcTicks}");
        var protectedBytes = _protector.Protect(Encoding.UTF8.GetBytes(payload));
        var cookieValue = WebSafeToBase64(protectedBytes);

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = session.ExpiresAt,
            IsEssential = true,
            Path = "/",
        };
        context.Response.Cookies.Append(_options.GuestSessionCookieName, cookieValue, options);
    }

    private static string WebSafeToBase64(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        return s.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static byte[] WebSafeFromBase64(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        // Validate before Convert so a tampered string throws FormatException rather
        // than a CryptographicException further down — caught by TryRead either way.
        if (!Base64.IsValid(padded))
        {
            throw new FormatException("Invalid base-64 cookie payload.");
        }
        return Convert.FromBase64String(padded);
    }
}
