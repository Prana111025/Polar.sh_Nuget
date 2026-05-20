using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Cart;
using PolarSharp.EcommerceStorefronts.Abstractions.Customers;
using PolarSharp.EcommerceStorefronts.Abstractions.Paging;
using PolarSharp.EcommerceStorefronts.Customers;
using PolarSharp.EcommerceStorefronts.Tests.TestSupport;

namespace PolarSharp.EcommerceStorefronts.Tests.Customers;

public sealed class DefaultStorefrontCustomerServiceTests
{
    [Fact]
    public async Task GetProfile_returns_AuthenticationError_for_guest()
    {
        var svc = new DefaultStorefrontCustomerService(
            TestStorefrontIdentityProvider.GuestSingleTenant(),
            new NullStorefrontCustomerSource());

        var result = await svc.GetProfileAsync(default);

        Assert.IsType<StorefrontAuthenticationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task GetProfile_forwards_to_source_for_authenticated_customer()
    {
        var customerId = Guid.NewGuid();
        var source = new RecordingCustomerSource(customerId);
        var svc = new DefaultStorefrontCustomerService(
            TestStorefrontIdentityProvider.SignedInSingleTenant(customerId),
            source);

        var result = await svc.GetProfileAsync(default);

        Assert.True(result.IsSuccess);
        Assert.Equal(customerId, source.LastCustomerId);
    }

    [Fact]
    public async Task UpdateProfile_succeeds_for_authenticated_customer()
    {
        var customerId = Guid.NewGuid();
        var source = new RecordingCustomerSource(customerId);
        var svc = new DefaultStorefrontCustomerService(
            TestStorefrontIdentityProvider.SignedInSingleTenant(customerId),
            source);

        var result = await svc.UpdateProfileAsync(new UpdateProfileCommand { DisplayName = "Test" }, default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ListOrders_returns_AuthenticationError_for_guest()
    {
        var svc = new DefaultStorefrontCustomerService(
            TestStorefrontIdentityProvider.GuestSingleTenant(),
            new NullStorefrontCustomerSource());
        var result = await svc.ListOrdersAsync(new ListOrdersQuery(), default);
        Assert.IsType<StorefrontAuthenticationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task GetOrder_returns_AuthenticationError_for_guest()
    {
        var svc = new DefaultStorefrontCustomerService(
            TestStorefrontIdentityProvider.GuestSingleTenant(),
            new NullStorefrontCustomerSource());
        var result = await svc.GetOrderAsync("o1", default);
        Assert.IsType<StorefrontAuthenticationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task ListAddresses_returns_AuthenticationError_for_guest()
    {
        var svc = new DefaultStorefrontCustomerService(
            TestStorefrontIdentityProvider.GuestSingleTenant(),
            new NullStorefrontCustomerSource());
        var result = await svc.ListAddressesAsync(default);
        Assert.IsType<StorefrontAuthenticationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task SaveAddress_returns_AuthenticationError_for_guest()
    {
        var svc = new DefaultStorefrontCustomerService(
            TestStorefrontIdentityProvider.GuestSingleTenant(),
            new NullStorefrontCustomerSource());
        var address = new SavedAddress
        {
            Id = "a1",
            Address = new ShippingAddress
            {
                FullName = "x", Line1 = "1", City = "c", PostalCode = "00000", CountryCode = "US",
            },
        };
        var result = await svc.SaveAddressAsync(address, default);
        Assert.IsType<StorefrontAuthenticationError>(result.Match(_ => (StorefrontError?)null, e => e));
    }

    [Fact]
    public async Task GetWalletBalance_returns_zero_balance_for_guest_without_error()
    {
        var svc = new DefaultStorefrontCustomerService(
            TestStorefrontIdentityProvider.GuestSingleTenant(),
            new NullStorefrontCustomerSource());

        var result = await svc.GetWalletBalanceAsync(default);

        Assert.True(result.IsSuccess);
        var balance = result.Match(b => b, _ => throw new InvalidOperationException());
        Assert.Equal(0, balance.AvailableCents);
    }

    [Fact]
    public async Task GetWalletBalance_forwards_to_source_for_authenticated_customer()
    {
        var customerId = Guid.NewGuid();
        var source = new RecordingCustomerSource(customerId);
        var svc = new DefaultStorefrontCustomerService(
            TestStorefrontIdentityProvider.SignedInSingleTenant(customerId),
            source);

        var result = await svc.GetWalletBalanceAsync(default);

        Assert.True(result.IsSuccess);
        Assert.Equal(customerId, source.LastWalletCustomerId);
    }

    private sealed class RecordingCustomerSource : IStorefrontCustomerSource
    {
        private readonly Guid _expectedId;
        public RecordingCustomerSource(Guid expectedId) => _expectedId = expectedId;
        public Guid? LastCustomerId { get; private set; }
        public Guid? LastWalletCustomerId { get; private set; }

        public Task<StorefrontResult<CustomerProfile>> GetProfileAsync(Guid customerId, CancellationToken ct)
        {
            LastCustomerId = customerId;
            return Task.FromResult(StorefrontResult<CustomerProfile>.Success(new CustomerProfile
            {
                Id = customerId,
                Email = "test@example.com",
                CreatedAt = DateTimeOffset.UtcNow,
            }));
        }

        public Task<StorefrontResult<StorefrontUnit>> UpdateProfileAsync(
            Guid customerId, UpdateProfileCommand cmd, CancellationToken ct) =>
            Task.FromResult(StorefrontResult<StorefrontUnit>.Success(StorefrontUnit.Value));

        public Task<StorefrontResult<PagedResult<OrderSummary>>> ListOrdersAsync(
            Guid customerId, ListOrdersQuery query, CancellationToken ct) =>
            Task.FromResult(StorefrontResult<PagedResult<OrderSummary>>.Success(new PagedResult<OrderSummary>
            {
                Rows = Array.Empty<OrderSummary>(),
                TotalCount = 0,
                Page = 0,
                PageSize = query.PageSize,
            }));

        public Task<StorefrontResult<OrderDetail>> GetOrderAsync(
            Guid customerId, string orderId, CancellationToken ct) =>
            Task.FromResult(StorefrontResult<OrderDetail>.Success(new OrderDetail
            {
                OrderId = orderId,
                OrderNumber = "ORD-1",
                Status = "paid",
                LineItems = Array.Empty<OrderLineItem>(),
                SubtotalCents = 0,
                TotalCents = 0,
                Currency = "USD",
                PlacedAt = DateTimeOffset.UtcNow,
            }));

        public Task<StorefrontResult<IReadOnlyList<SavedAddress>>> ListAddressesAsync(
            Guid customerId, CancellationToken ct) =>
            Task.FromResult(StorefrontResult<IReadOnlyList<SavedAddress>>.Success(Array.Empty<SavedAddress>()));

        public Task<StorefrontResult<SavedAddress>> SaveAddressAsync(
            Guid customerId, SavedAddress address, CancellationToken ct) =>
            Task.FromResult(StorefrontResult<SavedAddress>.Success(address));

        public Task<StorefrontResult<WalletBalance>> GetWalletBalanceAsync(Guid customerId, CancellationToken ct)
        {
            LastWalletCustomerId = customerId;
            return Task.FromResult(StorefrontResult<WalletBalance>.Success(new WalletBalance
            {
                AvailableCents = 12345,
                Currency = "USD",
                AsOf = DateTimeOffset.UtcNow,
            }));
        }
    }
}
