using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Customers;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;
using PolarSharp.EcommerceStorefronts.Abstractions.Paging;

namespace PolarSharp.EcommerceStorefronts.Customers;

/// <summary>
/// Default implementation of <see cref="IStorefrontCustomerService"/>. Gates every call
/// on <see cref="IStorefrontIdentityProvider.IsAuthenticated"/> and then forwards to
/// the registered <see cref="IStorefrontCustomerSource"/>.
/// </summary>
/// <remarks>
/// Guest sessions receive a <see cref="StorefrontAuthenticationError"/> so callers don't
/// have to branch on guest state at the UI layer. The wallet-balance method is the one
/// exception — it always succeeds, returning a zero balance for guests so account-area
/// chrome can render uniformly.
/// </remarks>
public sealed class DefaultStorefrontCustomerService : IStorefrontCustomerService
{
    private readonly IStorefrontIdentityProvider _identity;
    private readonly IStorefrontCustomerSource _source;
    private readonly TimeProvider _clock;

    /// <summary>Initialises the service.</summary>
    /// <param name="identity">Resolves the current customer.</param>
    /// <param name="source">The backing customer source.</param>
    /// <param name="clock">Clock used for diagnostics; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required argument is <see langword="null"/>.</exception>
    public DefaultStorefrontCustomerService(
        IStorefrontIdentityProvider identity,
        IStorefrontCustomerSource source,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(source);
        _identity = identity;
        _source = source;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public Task<StorefrontResult<CustomerProfile>> GetProfileAsync(CancellationToken ct) =>
        RequireAuthThenAsync(customerId => _source.GetProfileAsync(customerId, ct));

    /// <inheritdoc/>
    public Task<StorefrontResult<StorefrontUnit>> UpdateProfileAsync(
        UpdateProfileCommand cmd,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        return RequireAuthThenAsync(customerId => _source.UpdateProfileAsync(customerId, cmd, ct));
    }

    /// <inheritdoc/>
    public Task<StorefrontResult<PagedResult<OrderSummary>>> ListOrdersAsync(
        ListOrdersQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RequireAuthThenAsync(customerId => _source.ListOrdersAsync(customerId, query, ct));
    }

    /// <inheritdoc/>
    public Task<StorefrontResult<OrderDetail>> GetOrderAsync(string orderId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        return RequireAuthThenAsync(customerId => _source.GetOrderAsync(customerId, orderId, ct));
    }

    /// <inheritdoc/>
    public Task<StorefrontResult<IReadOnlyList<SavedAddress>>> ListAddressesAsync(CancellationToken ct) =>
        RequireAuthThenAsync(customerId => _source.ListAddressesAsync(customerId, ct));

    /// <inheritdoc/>
    public Task<StorefrontResult<SavedAddress>> SaveAddressAsync(SavedAddress address, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(address);
        return RequireAuthThenAsync(customerId => _source.SaveAddressAsync(customerId, address, ct));
    }

    /// <inheritdoc/>
    public Task<StorefrontResult<WalletBalance>> GetWalletBalanceAsync(CancellationToken ct)
    {
        if (!_identity.IsAuthenticated || !_identity.CurrentCustomerId.HasValue)
        {
            // Guest path: always zero balance so callers don't have to branch.
            return Task.FromResult(StorefrontResult<WalletBalance>.Success(new WalletBalance
            {
                AvailableCents = 0,
                Currency = "USD",
                AsOf = _clock.GetUtcNow(),
            }));
        }
        return _source.GetWalletBalanceAsync(
            _identity.CurrentCustomerId.GetValueOrDefault(Guid.Empty),
            ct);
    }

    private async Task<StorefrontResult<T>> RequireAuthThenAsync<T>(Func<Guid, Task<StorefrontResult<T>>> work)
    {
        if (!_identity.IsAuthenticated || !_identity.CurrentCustomerId.HasValue)
        {
            return StorefrontResult<T>.Failure(new StorefrontAuthenticationError(
                Message: "Customer self-service requires an authenticated customer.",
                CorrelationId: Guid.NewGuid().ToString("N")));
        }
        return await work(_identity.CurrentCustomerId.GetValueOrDefault(Guid.Empty)).ConfigureAwait(false);
    }
}
