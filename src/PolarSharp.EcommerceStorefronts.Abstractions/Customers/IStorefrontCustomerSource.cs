using PolarSharp.EcommerceStorefronts.Abstractions.Paging;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Customers;

/// <summary>
/// Read+write source for customer self-service data. Hosts wire an implementation that
/// bridges to their actual customer datastore (typically the PolarSharp customer
/// service + the reporting projection); the storefront-core default returns
/// <see cref="StorefrontNotFoundError"/> so the call surface stays usable on hosts that
/// have not yet plugged a real source.
/// </summary>
/// <remarks>
/// Lift-safe: lives in the abstractions package; types are the storefront-core DTOs
/// already defined in this assembly. The corresponding service
/// (<see cref="IStorefrontCustomerService"/>) sits ABOVE this source and adds the
/// authentication gating + the conventional <c>StorefrontUnit</c>-return shape used by
/// command-style operations.
/// </remarks>
public interface IStorefrontCustomerSource
{
    /// <summary>Loads <paramref name="customerId"/>'s profile.</summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The profile, or a <see cref="StorefrontNotFoundError"/> when unknown.</returns>
    Task<StorefrontResult<CustomerProfile>> GetProfileAsync(Guid customerId, CancellationToken ct);

    /// <summary>Updates <paramref name="customerId"/>'s profile.</summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="cmd">Profile changes; null fields are ignored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="StorefrontUnit.Value"/> on success.</returns>
    Task<StorefrontResult<StorefrontUnit>> UpdateProfileAsync(
        Guid customerId,
        UpdateProfileCommand cmd,
        CancellationToken ct);

    /// <summary>Lists <paramref name="customerId"/>'s orders, paged.</summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="query">Paging / filter parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of order summaries.</returns>
    Task<StorefrontResult<PagedResult<OrderSummary>>> ListOrdersAsync(
        Guid customerId,
        ListOrdersQuery query,
        CancellationToken ct);

    /// <summary>Returns one order's full detail.</summary>
    /// <param name="customerId">The customer identifier (used for authorization).</param>
    /// <param name="orderId">The Polar order identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The order detail.</returns>
    Task<StorefrontResult<OrderDetail>> GetOrderAsync(
        Guid customerId,
        string orderId,
        CancellationToken ct);

    /// <summary>Lists <paramref name="customerId"/>'s saved addresses.</summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All saved addresses.</returns>
    Task<StorefrontResult<IReadOnlyList<SavedAddress>>> ListAddressesAsync(
        Guid customerId,
        CancellationToken ct);

    /// <summary>Inserts or updates a saved address.</summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="address">The address to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The persisted address.</returns>
    Task<StorefrontResult<SavedAddress>> SaveAddressAsync(
        Guid customerId,
        SavedAddress address,
        CancellationToken ct);

    /// <summary>Returns the customer's prepaid wallet balance.</summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current balance; a zero-balance record when the customer has no wallet.</returns>
    Task<StorefrontResult<WalletBalance>> GetWalletBalanceAsync(Guid customerId, CancellationToken ct);
}
