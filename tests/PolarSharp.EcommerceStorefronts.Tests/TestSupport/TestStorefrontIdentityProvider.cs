using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;

namespace PolarSharp.EcommerceStorefronts.Tests.TestSupport;

internal sealed class TestStorefrontIdentityProvider : IStorefrontIdentityProvider
{
    public StorefrontOption<Guid> CurrentCustomerId { get; init; } = StorefrontOption<Guid>.None;
    public StorefrontOption<Guid> CurrentTenantId { get; init; } = StorefrontOption<Guid>.None;
    public bool IsMultiTenantMode { get; init; }
    public bool IsAuthenticated => CurrentCustomerId.HasValue;
    public bool IsGuest => !IsAuthenticated;

    public static TestStorefrontIdentityProvider SignedInSingleTenant(Guid customerId) => new()
    {
        CurrentCustomerId = StorefrontOption<Guid>.Some(customerId),
        IsMultiTenantMode = false,
    };

    public static TestStorefrontIdentityProvider GuestSingleTenant() => new()
    {
        IsMultiTenantMode = false,
    };

    public static TestStorefrontIdentityProvider SignedInMultiTenant(Guid customerId, Guid tenantId) => new()
    {
        CurrentCustomerId = StorefrontOption<Guid>.Some(customerId),
        CurrentTenantId = StorefrontOption<Guid>.Some(tenantId),
        IsMultiTenantMode = true,
    };

    public static TestStorefrontIdentityProvider GuestMultiTenant(Guid tenantId) => new()
    {
        CurrentTenantId = StorefrontOption<Guid>.Some(tenantId),
        IsMultiTenantMode = true,
    };
}
