using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Customers;
using PolarSharp.EcommerceStorefronts.Abstractions.Paging;

namespace PolarSharp.EcommerceStorefronts.Customers;

/// <summary>
/// <see cref="IStorefrontCustomerSource"/> that returns <see cref="StorefrontNotFoundError"/>
/// for every query and succeeds (without persisting) on every mutation. Registered as
/// the storefront-core default so the DI graph composes cleanly on hosts that have not
/// yet wired a real source.
/// </summary>
/// <remarks>
/// Replace this in production hosts by registering a real source (typically
/// <c>PolarSharp.EcommerceStorefronts.Polar.Reporting</c> for the read side +
/// <c>PolarSharp.EcommerceStorefronts.Polar.Identity</c> for the write side) BEFORE
/// calling <c>AddPolarStorefrontsCore</c>.
/// </remarks>
public sealed class NullStorefrontCustomerSource : IStorefrontCustomerSource
{
    private static StorefrontResult<T> NotFound<T>() => StorefrontResult<T>.Failure(
        new StorefrontNotFoundError(
            Message: "No customer source is registered. Register a real IStorefrontCustomerSource before calling AddPolarStorefrontsCore.",
            CorrelationId: Guid.NewGuid().ToString("N")));

    /// <inheritdoc/>
    public Task<StorefrontResult<CustomerProfile>> GetProfileAsync(Guid customerId, CancellationToken ct) =>
        Task.FromResult(NotFound<CustomerProfile>());

    /// <inheritdoc/>
    public Task<StorefrontResult<StorefrontUnit>> UpdateProfileAsync(
        Guid customerId,
        UpdateProfileCommand cmd,
        CancellationToken ct) =>
        Task.FromResult(StorefrontResult<StorefrontUnit>.Success(StorefrontUnit.Value));

    /// <inheritdoc/>
    public Task<StorefrontResult<PagedResult<OrderSummary>>> ListOrdersAsync(
        Guid customerId,
        ListOrdersQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var empty = StorefrontResult<PagedResult<OrderSummary>>.Success(new PagedResult<OrderSummary>
        {
            Rows = Array.Empty<OrderSummary>(),
            TotalCount = 0,
            Page = query.Page,
            PageSize = query.PageSize,
        });
        return Task.FromResult(empty);
    }

    /// <inheritdoc/>
    public Task<StorefrontResult<OrderDetail>> GetOrderAsync(
        Guid customerId,
        string orderId,
        CancellationToken ct) =>
        Task.FromResult(NotFound<OrderDetail>());

    /// <inheritdoc/>
    public Task<StorefrontResult<IReadOnlyList<SavedAddress>>> ListAddressesAsync(
        Guid customerId,
        CancellationToken ct) =>
        Task.FromResult(StorefrontResult<IReadOnlyList<SavedAddress>>.Success(Array.Empty<SavedAddress>()));

    /// <inheritdoc/>
    public Task<StorefrontResult<SavedAddress>> SaveAddressAsync(
        Guid customerId,
        SavedAddress address,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(address);
        return Task.FromResult(StorefrontResult<SavedAddress>.Success(address));
    }

    /// <inheritdoc/>
    public Task<StorefrontResult<WalletBalance>> GetWalletBalanceAsync(Guid customerId, CancellationToken ct) =>
        Task.FromResult(StorefrontResult<WalletBalance>.Success(new WalletBalance
        {
            AvailableCents = 0,
            Currency = "USD",
            AsOf = DateTimeOffset.UtcNow,
        }));
}
