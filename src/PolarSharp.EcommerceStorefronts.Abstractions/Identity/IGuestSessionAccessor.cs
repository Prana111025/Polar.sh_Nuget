namespace PolarSharp.EcommerceStorefronts.Abstractions.Identity;

/// <summary>
/// Mode-agnostic accessor for the ambient guest session identifier. Lets the cart
/// service resolve the owner of a guest cart without taking a hard dependency on
/// ASP.NET Core or on the <c>PolarSharp.EcommerceStorefronts.GuestSessions</c> package.
/// </summary>
/// <remarks>
/// The <c>GuestSessions</c> package supplies an HTTP-backed implementation that reads
/// the resolved session off <see cref="System.Collections.Generic.IDictionary{TKey,TValue}"/>
/// items on the current request. Hosts that disable guest sessions (for example a
/// fully-authenticated-only B2B storefront) register the
/// <c>NullGuestSessionAccessor</c> default in the storefront-core package, which
/// always returns <see cref="StorefrontOption{T}.None"/>.
/// <para>
/// Lift-safe: the abstraction lives in the storefront-core abstractions package with
/// no ASP.NET Core dependency.
/// </para>
/// </remarks>
public interface IGuestSessionAccessor
{
    /// <summary>
    /// The current request's guest session identifier, when one has been resolved.
    /// <see cref="StorefrontOption{T}.None"/> when there is no ambient request or no
    /// guest session was found.
    /// </summary>
    StorefrontOption<Guid> CurrentGuestSessionId { get; }
}
