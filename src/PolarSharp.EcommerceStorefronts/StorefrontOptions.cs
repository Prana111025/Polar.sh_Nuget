namespace PolarSharp.EcommerceStorefronts;

/// <summary>
/// Tunables for the storefront feature, bound by
/// <c>AddPolarStorefronts(Action&lt;StorefrontOptions&gt;)</c>.
/// </summary>
/// <remarks>
/// Defaults are sensible for a small-to-mid SaaS storefront. Production deployments
/// typically only override <see cref="GuestSessionLifetime"/> and the cart-limit
/// values when load testing surfaces a need.
/// </remarks>
public sealed class StorefrontOptions
{
    /// <summary>Name of the cookie that carries the guest session identifier.</summary>
    public string GuestSessionCookieName { get; set; } = "polar_guest_session";

    /// <summary>How long a guest session is honoured before re-creation.</summary>
    public TimeSpan GuestSessionLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Hard cap on the number of distinct lines in a single cart.</summary>
    public int MaxCartLineItems { get; set; } = 100;

    /// <summary>
    /// Hard cap on the grand total of a single cart in minor units; default is
    /// ten thousand US dollars (<c>1_000_000</c>).
    /// </summary>
    public int MaxCartTotalValueCents { get; set; } = 1_000_000;

    /// <summary>HTTP header carrying the cart idempotency token.</summary>
    public string CartIdempotencyTokenHeader { get; set; } = "X-Storefront-Idempotency";

    /// <summary>
    /// How long a saved cart is honoured before it is treated as expired. Cart loads
    /// past this window return None from the store (a fresh empty cart is created on
    /// the next call). Default matches <see cref="GuestSessionLifetime"/> so a guest
    /// cart and the cookie that owns it expire together.
    /// </summary>
    public TimeSpan CartLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// How long an idempotency-keyed response is replayable. A retry within this
    /// window short-circuits to the original response; after this window the cache
    /// forgets the key. Defaults to 24 hours — long enough to span any reasonable
    /// network-blip retry, short enough that the same idempotency key reused days
    /// later is treated as a fresh request.
    /// </summary>
    public TimeSpan IdempotencyCacheTtl { get; set; } = TimeSpan.FromHours(24);
}
