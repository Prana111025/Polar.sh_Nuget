using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;

namespace PolarSharp.EcommerceStorefronts.Identity;

/// <summary>
/// <see cref="IGuestSessionAccessor"/> that always returns
/// <see cref="StorefrontOption{T}.None"/>. Registered as the storefront-core default;
/// the <c>PolarSharp.EcommerceStorefronts.GuestSessions</c> package replaces it with
/// an HTTP-context-backed accessor when the host opts into guest sessions.
/// </summary>
/// <remarks>
/// Suitable for fully-authenticated-only deployments (B2B portals, internal tools)
/// where there are no anonymous shoppers and the cart service is always called by a
/// signed-in customer.
/// </remarks>
public sealed class NullGuestSessionAccessor : IGuestSessionAccessor
{
    /// <inheritdoc/>
    public StorefrontOption<Guid> CurrentGuestSessionId => StorefrontOption<Guid>.None;
}
