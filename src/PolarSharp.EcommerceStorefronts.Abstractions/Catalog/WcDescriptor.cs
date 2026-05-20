using System.Collections.Generic;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// Agent-consumable descriptor for a single PolarSharp Web Component. Mirrors the YAML
/// frontmatter spec from PLAN.md's v1.4.0 Phase 3 design so an agent constructing a tenant
/// marketplace can mechanically filter the catalog by deployment context, audience,
/// decision-tree tags, required tenant configuration, ship target, and accessibility posture.
/// </summary>
/// <remarks>
/// The descriptor is the contract; the rich HTML/Markdown documentation in the per-WC
/// README + DocFX article is for human readers. The descriptor SHOULD NOT be expanded with
/// new properties without bumping <c>PolarSharp.EcommerceStorefronts.Abstractions</c>'s
/// MAJOR version — agents and tooling depend on its shape.
/// </remarks>
public sealed record WcDescriptor
{
    /// <summary>The WC tag name (e.g. <c>polar-product-card</c>).</summary>
    public required string Name { get; init; }

    /// <summary>One-sentence purpose summary.</summary>
    public required string Purpose { get; init; }

    /// <summary>
    /// The primary data source the WC reads from (e.g.
    /// <c>IStorefrontCatalogProvider.GetProductAsync</c>); empty when the WC is purely
    /// presentational and reads no data.
    /// </summary>
    public required string DataSource { get; init; }

    /// <summary>
    /// New PolarSharp backend features the WC depends on (e.g. loyalty system,
    /// referral system, media-and-file-storage). Empty when the WC only needs already-shipped
    /// infrastructure.
    /// </summary>
    public required IReadOnlyList<string> BackendDependencies { get; init; }

    /// <summary>WCs that are commonly paired with this WC on the same page.</summary>
    public required IReadOnlyList<string> ComposesWith { get; init; }

    /// <summary>WCs that should not coexist with this WC on the same page (e.g. duplicate cart UIs).</summary>
    public required IReadOnlyList<string> ConflictsWith { get; init; }

    /// <summary>
    /// Tenant-side configuration the WC requires to function (e.g. "catalog must have
    /// at least one published product", "AI credentials validated").
    /// </summary>
    public required IReadOnlyList<string> RequiredTenantConfig { get; init; }

    /// <summary>Authentication-state requirement.</summary>
    public required WcAudience Audience { get; init; }

    /// <summary>Where the WC may legitimately be deployed.</summary>
    public required WcDeploymentContext Deployment { get; init; }

    /// <summary>Release-window assignment for the WC.</summary>
    public required WcShipTarget ShipTarget { get; init; }

    /// <summary>
    /// Optional clarifier for the ship target when a single WC has split delivery
    /// (e.g. <c>polar-product-360-viewer</c>: spin-frames mode v1.4.0, 3D-model mode v1.5+).
    /// </summary>
    public string? ShipTargetNote { get; init; }

    /// <summary>
    /// Agent-side filter tags identifying the marketplace niches the WC is relevant for
    /// (e.g. <c>physical-goods</c>, <c>digital-goods</c>, <c>subscriptions</c>, <c>b2b</c>,
    /// <c>b2c</c>, <c>fashion</c>, <c>electronics</c>).
    /// </summary>
    public required IReadOnlyList<string> DecisionTreeTags { get; init; }

    /// <summary>Custom DOM events the WC emits (e.g. <c>polarAddToCartTriggered</c>).</summary>
    public required IReadOnlyList<string> EmitsEvents { get; init; }

    /// <summary>Custom DOM events the WC subscribes to (e.g. <c>polarVariantChanged</c>).</summary>
    public required IReadOnlyList<string> ListensToEvents { get; init; }

    /// <summary>Accessibility metadata for the WC.</summary>
    public required WcAccessibility Accessibility { get; init; }
}
