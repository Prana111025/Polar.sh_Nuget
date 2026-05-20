using PolarSharp.EcommerceStorefronts.Abstractions;
using PolarSharp.EcommerceStorefronts.Abstractions.Identity;

namespace PolarSharp.EcommerceStorefronts.Identity;

/// <summary>
/// <see cref="IStorefrontIdentityProvider"/> default — single-tenant, no authenticated
/// customer. Registered when the host has not plugged a real identity bridge so the
/// DI graph composes cleanly on bare-bones smoke-test hosts.
/// </summary>
/// <remarks>
/// Real hosts swap this out by registering an identity bridge BEFORE calling
/// <c>AddPolarStorefrontsCore</c>:
/// <list type="bullet">
/// <item>Single-tenant: an ASP.NET Core Identity-backed implementation.</item>
/// <item>Multi-tenant: <c>PolarSharp.MultiTenant.Identity</c> with the
/// <c>PolarSharp.EcommerceStorefronts.Polar.Identity</c> bridge.</item>
/// </list>
/// </remarks>
public sealed class AnonymousSingleTenantIdentityProvider : IStorefrontIdentityProvider
{
    /// <inheritdoc/>
    public StorefrontOption<Guid> CurrentCustomerId => StorefrontOption<Guid>.None;

    /// <inheritdoc/>
    public StorefrontOption<Guid> CurrentTenantId => StorefrontOption<Guid>.None;

    /// <inheritdoc/>
    public bool IsMultiTenantMode => false;

    /// <inheritdoc/>
    public bool IsAuthenticated => false;

    /// <inheritdoc/>
    public bool IsGuest => true;
}
