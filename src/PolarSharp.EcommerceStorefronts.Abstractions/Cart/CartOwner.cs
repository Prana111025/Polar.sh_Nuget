namespace PolarSharp.EcommerceStorefronts.Abstractions.Cart;

/// <summary>
/// Discriminator describing who owns a cart — either a signed-in customer or a guest
/// session.
/// </summary>
/// <remarks>
/// Carts are ALWAYS owned by exactly one of the two; promoting a guest cart to a customer
/// cart at sign-up creates a new owner record with the same identifier carried over.
/// Modelled as a discriminated union via a readonly record struct so equality and
/// hashing are value-based and pattern matching is exhaustive.
/// </remarks>
public readonly record struct CartOwner
{
    private CartOwner(Guid id, CartOwnerKind kind)
    {
        Id = id;
        Kind = kind;
    }

    /// <summary>The identifier of the owning entity (customer or guest session).</summary>
    public Guid Id { get; }

    /// <summary>Discriminator selecting which entity <see cref="Id"/> refers to.</summary>
    public CartOwnerKind Kind { get; }

    /// <summary>Creates a cart owner referring to an authenticated customer.</summary>
    /// <param name="customerId">The customer's identifier.</param>
    /// <returns>An owner discriminator in <see cref="CartOwnerKind.Customer"/> form.</returns>
    public static CartOwner FromCustomer(Guid customerId) =>
        new(customerId, CartOwnerKind.Customer);

    /// <summary>Creates a cart owner referring to an anonymous guest session.</summary>
    /// <param name="guestSessionId">The guest session identifier.</param>
    /// <returns>An owner discriminator in <see cref="CartOwnerKind.Guest"/> form.</returns>
    public static CartOwner FromGuest(Guid guestSessionId) =>
        new(guestSessionId, CartOwnerKind.Guest);
}

/// <summary>Discriminator value for <see cref="CartOwner"/>.</summary>
public enum CartOwnerKind
{
    /// <summary>The cart is owned by an anonymous guest session.</summary>
    Guest = 0,

    /// <summary>The cart is owned by an authenticated customer.</summary>
    Customer = 1,
}
