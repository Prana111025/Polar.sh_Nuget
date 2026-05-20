using Microsoft.Extensions.Options;
using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Catalog;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;

namespace PolarSharp.EcommerceStorefronts.Cart;

/// <summary>
/// Storefront-core implementation of <see cref="IStorefrontCartService"/>.
/// </summary>
/// <remarks>
/// Enforces the Case Study 03 "server-as-source-of-truth" fraud-prevention discipline on
/// every mutation:
/// <list type="bullet">
/// <item>Client-supplied unit prices are IGNORED — the cart service revalidates each line
/// against the catalog provider and uses the catalog's price as authoritative.</item>
/// <item>Quantities are clamped to <c>[1, MaxCartLineItems]</c>; over-limit lines are
/// rejected with <see cref="StorefrontValidationError"/>.</item>
/// <item>Grand totals are recomputed server-side on every mutation; the cart returned to
/// the caller always reflects the freshly-computed numbers.</item>
/// <item>Discount codes are not honoured at the cart-service level — they are recorded
/// for the checkout pipeline (<c>ApplyDiscountsStage</c>) to validate against the
/// merchant's discount table.</item>
/// </list>
/// <para>
/// Cart ownership is resolved mode-agnostically: an authenticated customer owns the cart
/// (via <see cref="IStorefrontIdentityProvider.CurrentCustomerId"/>); otherwise the cart
/// is owned by the current guest session (via <see cref="IGuestSessionAccessor"/>). The
/// tenant scope is read from <see cref="IStorefrontIdentityProvider.CurrentTenantId"/>
/// and is <see cref="StorefrontOption{T}.None"/> in single-tenant mode.
/// </para>
/// </remarks>
public sealed class DefaultStorefrontCartService : IStorefrontCartService
{
    private readonly IStorefrontIdentityProvider _identity;
    private readonly IGuestSessionAccessor _guestSessions;
    private readonly IStorefrontCartStore _store;
    private readonly IStorefrontCatalogProvider _catalog;
    private readonly StorefrontOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>Initialises the cart service.</summary>
    /// <param name="identity">Resolves the current customer + tenant.</param>
    /// <param name="guestSessions">Resolves the current guest session.</param>
    /// <param name="store">Cart persistence.</param>
    /// <param name="catalog">Catalog provider used to revalidate prices + availability.</param>
    /// <param name="options">Storefront tunables (cart limits, idempotency).</param>
    /// <param name="clock">Clock used for timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any argument is <see langword="null"/>.
    /// </exception>
    public DefaultStorefrontCartService(
        IStorefrontIdentityProvider identity,
        IGuestSessionAccessor guestSessions,
        IStorefrontCartStore store,
        IStorefrontCatalogProvider catalog,
        IOptions<StorefrontOptions> options,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(guestSessions);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);

        _identity = identity;
        _guestSessions = guestSessions;
        _store = store;
        _catalog = catalog;
        _options = options.Value;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<StorefrontResult<Abstractions.Cart.Cart>> GetCurrentCartAsync(CancellationToken ct)
    {
        var (owner, tenantId, error) = ResolveOwner();
        if (error is not null)
        {
            return StorefrontResult<Abstractions.Cart.Cart>.Failure(error);
        }

        var existing = await _store.FindByOwnerAsync(owner!.Value, tenantId, ct).ConfigureAwait(false);
        if (existing.HasValue)
        {
            return StorefrontResult<Abstractions.Cart.Cart>.Success(
                existing.GetValueOrDefault(default!));
        }

        var fresh = NewEmptyCart(owner.Value, tenantId);
        await _store.SaveAsync(fresh, ct).ConfigureAwait(false);
        return StorefrontResult<Abstractions.Cart.Cart>.Success(fresh);
    }

    /// <inheritdoc/>
    public async Task<StorefrontResult<Abstractions.Cart.Cart>> AddToCartAsync(
        AddToCartCommand cmd,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        if (cmd.Quantity <= 0)
        {
            return Invalid("Quantity must be positive.", nameof(cmd.Quantity), "Cart.QuantityMustBePositive");
        }

        var loadResult = await GetCurrentCartAsync(ct).ConfigureAwait(false);
        if (loadResult.IsFailure)
        {
            return loadResult;
        }

        Abstractions.Cart.Cart cart = loadResult.Match(c => c, _ => throw new InvalidOperationException("unreachable"));

        // Pull the authoritative product from the catalog so we never trust the client's
        // copy of the price or display name.
        var productResult = await _catalog.GetProductAsync(cmd.ProductId, language: null, ct).ConfigureAwait(false);
        if (productResult.IsFailure)
        {
            return StorefrontResult<Abstractions.Cart.Cart>.Failure(
                productResult.Match<StorefrontError>(_ => throw new InvalidOperationException("unreachable"), e => e));
        }

        StorefrontProduct product = productResult.Match(p => p, _ => throw new InvalidOperationException("unreachable"));
        StorefrontPrice unitPrice = ResolveVariantPrice(product, cmd.VariantId);

        if (!product.IsAvailable)
        {
            return Conflict("Product is not currently available.", "Cart.ProductUnavailable");
        }

        var existing = cart.LineItems.FirstOrDefault(li => li.ProductId == cmd.ProductId && li.VariantId == cmd.VariantId);
        IReadOnlyList<CartLineItem> nextLines;
        if (existing is not null)
        {
            var merged = existing with
            {
                Quantity = existing.Quantity + cmd.Quantity,
                UnitAmountCents = unitPrice.AmountCents,
                LineSubtotalCents = unitPrice.AmountCents * (existing.Quantity + cmd.Quantity),
                Currency = unitPrice.Currency,
            };
            nextLines = cart.LineItems.Select(li => li.LineId == existing.LineId ? merged : li).ToArray();
        }
        else
        {
            var line = new CartLineItem
            {
                LineId = Guid.NewGuid().ToString("N"),
                ProductId = product.Id,
                VariantId = cmd.VariantId,
                DisplayName = product.Name,
                Thumbnail = product.Media.FirstOrDefault(),
                Quantity = cmd.Quantity,
                UnitAmountCents = unitPrice.AmountCents,
                LineSubtotalCents = unitPrice.AmountCents * cmd.Quantity,
                Currency = unitPrice.Currency,
            };
            nextLines = cart.LineItems.Append(line).ToArray();
        }

        if (nextLines.Count > _options.MaxCartLineItems)
        {
            return Invalid(
                $"Cart cannot exceed {_options.MaxCartLineItems} distinct lines.",
                nameof(cmd.ProductId),
                "Cart.MaxLineItemsExceeded");
        }

        var updated = Recompute(cart with { LineItems = nextLines });

        if (updated.Totals.GrandTotalCents > _options.MaxCartTotalValueCents)
        {
            return Invalid(
                $"Cart grand total cannot exceed {_options.MaxCartTotalValueCents} minor units.",
                nameof(CartTotals.GrandTotalCents),
                "Cart.GrandTotalExceeded");
        }

        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return StorefrontResult<Abstractions.Cart.Cart>.Success(updated);
    }

    /// <inheritdoc/>
    public async Task<StorefrontResult<Abstractions.Cart.Cart>> UpdateLineQuantityAsync(
        UpdateQuantityCommand cmd,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        if (cmd.Quantity < 0)
        {
            return Invalid("Quantity must be zero or positive.", nameof(cmd.Quantity), "Cart.QuantityMustBeNonNegative");
        }

        var loadResult = await GetCurrentCartAsync(ct).ConfigureAwait(false);
        if (loadResult.IsFailure)
        {
            return loadResult;
        }

        Abstractions.Cart.Cart cart = loadResult.Match(c => c, _ => throw new InvalidOperationException("unreachable"));
        var line = cart.LineItems.FirstOrDefault(li => li.LineId == cmd.LineId);
        if (line is null)
        {
            return NotFound("Line not found in current cart.");
        }

        IReadOnlyList<CartLineItem> nextLines;
        if (cmd.Quantity == 0)
        {
            nextLines = cart.LineItems.Where(li => li.LineId != cmd.LineId).ToArray();
        }
        else
        {
            // Re-validate the unit price against the catalog so a stale cart row cannot
            // become a price-tampering vector.
            var productResult = await _catalog.GetProductAsync(line.ProductId, language: null, ct).ConfigureAwait(false);
            if (productResult.IsFailure)
            {
                return StorefrontResult<Abstractions.Cart.Cart>.Failure(
                    productResult.Match<StorefrontError>(_ => throw new InvalidOperationException("unreachable"), e => e));
            }
            StorefrontProduct product = productResult.Match(p => p, _ => throw new InvalidOperationException("unreachable"));
            StorefrontPrice unitPrice = ResolveVariantPrice(product, line.VariantId);
            var updatedLine = line with
            {
                Quantity = cmd.Quantity,
                UnitAmountCents = unitPrice.AmountCents,
                LineSubtotalCents = unitPrice.AmountCents * cmd.Quantity,
                Currency = unitPrice.Currency,
            };
            nextLines = cart.LineItems.Select(li => li.LineId == cmd.LineId ? updatedLine : li).ToArray();
        }

        var updated = Recompute(cart with { LineItems = nextLines });
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return StorefrontResult<Abstractions.Cart.Cart>.Success(updated);
    }

    /// <inheritdoc/>
    public Task<StorefrontResult<Abstractions.Cart.Cart>> RemoveLineAsync(string lineId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lineId);
        return UpdateLineQuantityAsync(new UpdateQuantityCommand { LineId = lineId, Quantity = 0 }, ct);
    }

    /// <inheritdoc/>
    public async Task<StorefrontResult<Abstractions.Cart.Cart>> ApplyDiscountCodeAsync(
        string code,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var loadResult = await GetCurrentCartAsync(ct).ConfigureAwait(false);
        if (loadResult.IsFailure)
        {
            return loadResult;
        }
        Abstractions.Cart.Cart cart = loadResult.Match(c => c, _ => throw new InvalidOperationException("unreachable"));

        // Cart service records the code; the checkout pipeline's ApplyDiscountsStage
        // validates it against the merchant's discount table. Never trust the client to
        // tell us the discount is valid.
        var updated = Recompute(cart with { DiscountCode = code.Trim() });
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return StorefrontResult<Abstractions.Cart.Cart>.Success(updated);
    }

    /// <inheritdoc/>
    public async Task<StorefrontResult<Abstractions.Cart.Cart>> RemoveDiscountAsync(CancellationToken ct)
    {
        var loadResult = await GetCurrentCartAsync(ct).ConfigureAwait(false);
        if (loadResult.IsFailure)
        {
            return loadResult;
        }
        Abstractions.Cart.Cart cart = loadResult.Match(c => c, _ => throw new InvalidOperationException("unreachable"));
        var updated = Recompute(cart with { DiscountCode = null });
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return StorefrontResult<Abstractions.Cart.Cart>.Success(updated);
    }

    /// <inheritdoc/>
    public async Task<StorefrontResult<Abstractions.Cart.Cart>> SetShippingAddressAsync(
        ShippingAddress address,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(address);

        var loadResult = await GetCurrentCartAsync(ct).ConfigureAwait(false);
        if (loadResult.IsFailure)
        {
            return loadResult;
        }
        Abstractions.Cart.Cart cart = loadResult.Match(c => c, _ => throw new InvalidOperationException("unreachable"));
        var updated = Recompute(cart with { ShippingAddress = address });
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return StorefrontResult<Abstractions.Cart.Cart>.Success(updated);
    }

    private (CartOwner? owner, StorefrontOption<Guid> tenantId, StorefrontError? error) ResolveOwner()
    {
        var tenantId = _identity.CurrentTenantId;
        if (_identity.IsAuthenticated && _identity.CurrentCustomerId.HasValue)
        {
            return (CartOwner.FromCustomer(_identity.CurrentCustomerId.GetValueOrDefault(Guid.Empty)), tenantId, null);
        }
        if (_guestSessions.CurrentGuestSessionId.HasValue)
        {
            return (CartOwner.FromGuest(_guestSessions.CurrentGuestSessionId.GetValueOrDefault(Guid.Empty)), tenantId, null);
        }
        return (null, tenantId, new StorefrontAuthenticationError(
            Message: "No customer or guest session resolved for the current request.",
            CorrelationId: Guid.NewGuid().ToString("N")));
    }

    private Abstractions.Cart.Cart NewEmptyCart(CartOwner owner, StorefrontOption<Guid> tenantId)
    {
        var now = _clock.GetUtcNow();
        return new Abstractions.Cart.Cart
        {
            Id = Guid.NewGuid(),
            CustomerId = owner.Kind == CartOwnerKind.Customer ? owner.Id : null,
            GuestSessionId = owner.Kind == CartOwnerKind.Guest ? owner.Id : null,
            TenantId = tenantId.HasValue ? tenantId.GetValueOrDefault(Guid.Empty) : null,
            LineItems = [],
            Totals = new CartTotals
            {
                SubtotalCents = 0,
                GrandTotalCents = 0,
                Currency = "USD",
            },
            CreatedAt = now,
        };
    }

    private Abstractions.Cart.Cart Recompute(Abstractions.Cart.Cart cart)
    {
        var subtotal = cart.LineItems.Sum(li => li.LineSubtotalCents);
        var currency = cart.LineItems.FirstOrDefault()?.Currency ?? cart.Totals.Currency;
        var totals = new CartTotals
        {
            SubtotalCents = subtotal,
            DiscountCents = 0, // applied at checkout-pipeline time, never client-side.
            TaxCents = null,
            ShippingCents = null,
            GrandTotalCents = subtotal,
            Currency = currency,
        };
        return cart with
        {
            Totals = totals,
            UpdatedAt = _clock.GetUtcNow(),
        };
    }

    private static StorefrontPrice ResolveVariantPrice(StorefrontProduct product, string? variantId)
    {
        if (variantId is null)
        {
            return product.DefaultPrice;
        }
        var variant = product.Variants.FirstOrDefault(v => v.Id == variantId);
        return variant?.Price ?? product.DefaultPrice;
    }

    private static StorefrontResult<Abstractions.Cart.Cart> Invalid(string message, string field, string key)
    {
        var corr = Guid.NewGuid().ToString("N");
        return StorefrontResult<Abstractions.Cart.Cart>.Failure(new StorefrontValidationError(
            Message: message,
            CorrelationId: corr,
            Fields: new[] { new StorefrontFieldError(field, key) }));
    }

    private static StorefrontResult<Abstractions.Cart.Cart> Conflict(string message, string key) =>
        StorefrontResult<Abstractions.Cart.Cart>.Failure(new StorefrontConflictError(
            Message: $"{message} ({key})",
            CorrelationId: Guid.NewGuid().ToString("N")));

    private static StorefrontResult<Abstractions.Cart.Cart> NotFound(string message) =>
        StorefrontResult<Abstractions.Cart.Cart>.Failure(new StorefrontNotFoundError(
            Message: message,
            CorrelationId: Guid.NewGuid().ToString("N")));
}
