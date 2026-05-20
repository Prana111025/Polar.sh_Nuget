using Microsoft.AspNetCore.Http;
using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;

namespace PolarSharp.EcommerceStorefronts.GuestSessions;

/// <summary>
/// <see cref="IGuestSessionAccessor"/> that reads the resolved
/// <see cref="GuestSession"/> off the current <see cref="HttpContext.Items"/>.
/// Registered by <see cref="Extensions.GuestSessionServiceCollectionExtensions.AddPolarGuestSessions"/>.
/// </summary>
/// <remarks>
/// The middleware (<see cref="GuestSessionMiddleware"/>) writes the session under
/// <see cref="GuestSessionMiddleware.HttpContextItemKey"/> early in the request
/// pipeline; this accessor reads it back so storefront-core services can know the
/// current guest session id without taking a hard ASP.NET Core dependency.
/// </remarks>
public sealed class HttpContextGuestSessionAccessor : IGuestSessionAccessor
{
    private readonly IHttpContextAccessor _http;

    /// <summary>Constructs the accessor.</summary>
    /// <param name="http">The ambient HTTP-context accessor.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="http"/> is <see langword="null"/>.</exception>
    public HttpContextGuestSessionAccessor(IHttpContextAccessor http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <inheritdoc/>
    public StorefrontOption<Guid> CurrentGuestSessionId
    {
        get
        {
            var ctx = _http.HttpContext;
            if (ctx?.Items[GuestSessionMiddleware.HttpContextItemKey] is GuestSession session)
            {
                return StorefrontOption<Guid>.Some(session.Id);
            }
            return StorefrontOption<Guid>.None;
        }
    }
}
