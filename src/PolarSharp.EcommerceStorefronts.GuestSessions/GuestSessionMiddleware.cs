using Microsoft.AspNetCore.Http;

namespace PolarSharp.EcommerceStorefronts.GuestSessions;

/// <summary>
/// ASP.NET Core middleware that resolves the current guest session at the start of
/// every request and attaches it to <see cref="HttpContext.Items"/> under
/// <see cref="HttpContextItemKey"/>.
/// </summary>
/// <remarks>
/// Down-stream code (cart service, checkout service) reads the session from
/// <c>HttpContext.Items[GuestSessionMiddleware.HttpContextItemKey]</c> via
/// <see cref="HttpContextGuestSessionAccessor"/> rather than re-parsing the cookie.
/// The middleware also renews the session on every request so active visitors do not
/// get bounced when the cookie nears expiry.
/// <para>
/// Place after <c>UseRouting()</c> and before any endpoint that reads the guest
/// session.
/// </para>
/// </remarks>
public sealed class GuestSessionMiddleware
{
    /// <summary>
    /// Key under which the resolved <see cref="GuestSession"/> is stored on
    /// <see cref="HttpContext.Items"/>.
    /// </summary>
    public const string HttpContextItemKey = "PolarSharp.GuestSession";

    private readonly RequestDelegate _next;

    /// <summary>Constructs the middleware.</summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> is <see langword="null"/>.</exception>
    public GuestSessionMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>Invoked once per request by the ASP.NET Core pipeline.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="guestSessions">The guest session service.</param>
    /// <returns>A task that completes when the inner pipeline completes.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> or <paramref name="guestSessions"/> is <see langword="null"/>.
    /// </exception>
    public Task InvokeAsync(HttpContext context, IGuestSessionService guestSessions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(guestSessions);

        var session = guestSessions.TryRead(context) ?? guestSessions.Create(context);
        session = guestSessions.Renew(context, session);
        context.Items[HttpContextItemKey] = session;
        return _next(context);
    }
}
