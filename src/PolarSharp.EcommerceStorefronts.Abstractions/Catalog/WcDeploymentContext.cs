namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// Where a PolarSharp Web Component may legitimately be deployed. The agent-driven
/// marketplace constructor uses this to avoid surfacing tenant-storefront-only WCs
/// (account menu, wallet balance, order history) on partner / third-party sites
/// where they would be confusing or unworkable without tenant session context.
/// </summary>
public enum WcDeploymentContext
{
    /// <summary>WC may be embedded on any site (tenant storefront, partner site, blog, aggregator).</summary>
    EmbedAnywhere = 0,

    /// <summary>WC may only be used on the tenant's own storefront where account state exists.</summary>
    TenantStorefrontOnly = 1,

    /// <summary>
    /// WC supports anonymous reads on any embed but writes require the tenant's own storefront context
    /// (e.g. product Q&amp;A / reviews where anyone may read but only verified purchasers may post).
    /// </summary>
    EmbedAnywhereReadTenantStorefrontWrite = 2,
}
