namespace PolarSharp.EcommerceStorefronts.Abstractions.Cart;

/// <summary>
/// Persistence seam for storefront carts. Implementations decide whether carts live in
/// memory, in EF Core, in a key-value store, or somewhere else — the service surface
/// above (<see cref="IStorefrontCartService"/>) is unaware of the choice.
/// </summary>
/// <remarks>
/// The store is keyed by an owner discriminator that resolves to either a customer
/// identifier (for authenticated carts) or a guest session identifier (for anonymous
/// carts) plus an optional tenant scope. Mode-agnostic per Case Study 05: when the
/// host is single-tenant the <c>tenantId</c> argument is
/// <see cref="StorefrontOption{T}.None"/> and the store simply ignores it.
/// <para>
/// Lift-safe: the store interface lives in the abstractions package and does not depend
/// on any provider-specific type.
/// </para>
/// </remarks>
public interface IStorefrontCartStore
{
    /// <summary>
    /// Returns the cart owned by <paramref name="owner"/>, or
    /// <see cref="StorefrontOption{T}.None"/> if none exists.
    /// </summary>
    /// <param name="owner">The owner discriminator.</param>
    /// <param name="tenantId">The tenant scope; <see cref="StorefrontOption{T}.None"/> in single-tenant mode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The persisted cart, when present.</returns>
    Task<StorefrontOption<Cart>> FindByOwnerAsync(
        CartOwner owner,
        StorefrontOption<Guid> tenantId,
        CancellationToken ct);

    /// <summary>Persists <paramref name="cart"/>, overwriting any previous state.</summary>
    /// <param name="cart">The cart to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the cart is durable.</returns>
    Task SaveAsync(Cart cart, CancellationToken ct);

    /// <summary>Removes the cart owned by <paramref name="owner"/>, if any.</summary>
    /// <param name="owner">The owner discriminator.</param>
    /// <param name="tenantId">The tenant scope; <see cref="StorefrontOption{T}.None"/> in single-tenant mode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the cart is removed (or was already absent).</returns>
    Task DeleteByOwnerAsync(
        CartOwner owner,
        StorefrontOption<Guid> tenantId,
        CancellationToken ct);

    /// <summary>
    /// Looks up a cart by its identifier; used by the checkout service to retrieve the
    /// originating cart for a session whose ownership has since changed (for example a
    /// guest who signed in mid-checkout).
    /// </summary>
    /// <param name="cartId">The cart identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The persisted cart, when present.</returns>
    Task<StorefrontOption<Cart>> FindByIdAsync(Guid cartId, CancellationToken ct);
}
