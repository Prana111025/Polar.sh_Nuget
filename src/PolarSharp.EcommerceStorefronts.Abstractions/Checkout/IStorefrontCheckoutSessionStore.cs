namespace PolarSharp.EcommerceStorefronts.Abstractions.Checkout;

/// <summary>
/// Persistence seam for checkout sessions. Hosts that want durable session lookup
/// across processes register a provider-backed implementation; the storefront-core
/// default keeps sessions in-process for development + small deployments.
/// </summary>
/// <remarks>
/// Sessions outlive a single HTTP request — the checkout pipeline may yield control to
/// an external payment redirect and resume later, so the storefront UI looks the
/// session back up by id. Lift-safe: lives in the abstractions package with no
/// provider coupling.
/// </remarks>
public interface IStorefrontCheckoutSessionStore
{
    /// <summary>Persists <paramref name="session"/>, overwriting any previous state for the same id.</summary>
    /// <param name="session">The session to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the session is durable.</returns>
    Task SaveAsync(CheckoutSession session, CancellationToken ct);

    /// <summary>
    /// Returns the session identified by <paramref name="sessionId"/>, or
    /// <see cref="StorefrontOption{T}.None"/> if unknown.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The session, when present.</returns>
    Task<StorefrontOption<CheckoutSession>> FindByIdAsync(Guid sessionId, CancellationToken ct);
}
