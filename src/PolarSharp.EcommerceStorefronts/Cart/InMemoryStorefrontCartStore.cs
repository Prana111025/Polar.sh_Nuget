using System.Collections.Concurrent;
using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;

namespace PolarSharp.EcommerceStorefronts.Cart;

/// <summary>
/// In-process <see cref="IStorefrontCartStore"/>. The default registration; suitable
/// for development hosts and small single-process deployments.
/// </summary>
/// <remarks>
/// Production multi-process hosts replace this with an EF Core / Redis-backed
/// implementation registered before <c>AddPolarStorefrontsCore</c>. The store is
/// thread-safe but does not survive a process restart — carts disappear on restart,
/// which is acceptable for guest carts (shoppers re-add) but not for authenticated
/// carts that the customer expects to persist across visits.
/// </remarks>
public sealed class InMemoryStorefrontCartStore : IStorefrontCartStore
{
    private readonly ConcurrentDictionary<string, Abstractions.Cart.Cart> _byOwner = new();
    private readonly ConcurrentDictionary<Guid, string> _ownerByCartId = new();

    /// <inheritdoc/>
    public Task<StorefrontOption<Abstractions.Cart.Cart>> FindByOwnerAsync(
        CartOwner owner,
        StorefrontOption<Guid> tenantId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key = BuildKey(owner, tenantId);
        var found = _byOwner.TryGetValue(key, out var cart)
            ? StorefrontOption<Abstractions.Cart.Cart>.Some(cart)
            : StorefrontOption<Abstractions.Cart.Cart>.None;
        return Task.FromResult(found);
    }

    /// <inheritdoc/>
    public Task SaveAsync(Abstractions.Cart.Cart cart, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cart);
        ct.ThrowIfCancellationRequested();

        var owner = ResolveOwner(cart);
        var tenantId = cart.TenantId.HasValue
            ? StorefrontOption<Guid>.Some(cart.TenantId.Value)
            : StorefrontOption<Guid>.None;
        var key = BuildKey(owner, tenantId);

        _byOwner[key] = cart;
        _ownerByCartId[cart.Id] = key;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteByOwnerAsync(
        CartOwner owner,
        StorefrontOption<Guid> tenantId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key = BuildKey(owner, tenantId);
        if (_byOwner.TryRemove(key, out var removed))
        {
            _ownerByCartId.TryRemove(removed.Id, out _);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<StorefrontOption<Abstractions.Cart.Cart>> FindByIdAsync(Guid cartId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_ownerByCartId.TryGetValue(cartId, out var key)
            && _byOwner.TryGetValue(key, out var cart))
        {
            return Task.FromResult(StorefrontOption<Abstractions.Cart.Cart>.Some(cart));
        }
        return Task.FromResult(StorefrontOption<Abstractions.Cart.Cart>.None);
    }

    private static CartOwner ResolveOwner(Abstractions.Cart.Cart cart)
    {
        if (cart.CustomerId.HasValue)
        {
            return CartOwner.FromCustomer(cart.CustomerId.Value);
        }
        if (cart.GuestSessionId.HasValue)
        {
            return CartOwner.FromGuest(cart.GuestSessionId.Value);
        }
        throw new InvalidOperationException(
            "Cart must have either CustomerId or GuestSessionId populated before persistence.");
    }

    private static string BuildKey(CartOwner owner, StorefrontOption<Guid> tenantId)
    {
        var tenantPart = tenantId.HasValue ? tenantId.GetValueOrDefault(Guid.Empty).ToString("N") : "_";
        return $"{(int)owner.Kind}|{owner.Id:N}|{tenantPart}";
    }
}
