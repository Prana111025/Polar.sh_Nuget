namespace PolarSharp.EcommerceStorefronts.Abstractions.Theming;

/// <summary>
/// Agent-consumable descriptor for a single PolarSharp design token. The descriptor is
/// the contract: agents constructing a tenant marketplace read it to know the token's
/// CSS variable name, friendly attribute alias (where present), default value, value type,
/// and component scope so they can mechanically generate per-tenant theme JSON without
/// reading the WC source.
/// </summary>
/// <remarks>
/// <para>
/// The full registry is exposed via <see cref="PolarThemeTokens.AllTokens"/>. Filters such
/// as <see cref="PolarThemeTokens.ByCategory(ThemeTokenCategory)"/> return the relevant
/// subset of descriptors.
/// </para>
/// <para>
/// Tokens fall into two attribute-exposure tiers: attribute-aliased (set per-element via
/// a friendly HTML attribute, e.g. <c>&lt;polar-product-card primary-color="#ff0000"&gt;</c>)
/// and CSS-variable-only (set per-tenant via the SignalR-injected <c>:root</c> stylesheet).
/// The precedence chain is: WC bundle defaults &lt; tenant theme (SignalR-injected) &lt;
/// host page CSS &lt; inline attribute. See the Phase 3 theming-token catalog in PLAN.md
/// for the design rationale.
/// </para>
/// </remarks>
public sealed record PolarThemeTokenDescriptor
{
    /// <summary>
    /// The CSS custom property name (e.g. <c>--polar-primary</c>). Every token name
    /// starts with the <c>--polar-</c> prefix.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The token's functional category.</summary>
    public required ThemeTokenCategory Category { get; init; }

    /// <summary>One-sentence purpose summary suitable for editor tooltips.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// <see langword="true"/> when the token can be overridden per-element via a friendly
    /// HTML attribute alias on a WC; <see langword="false"/> when the token is exposed only
    /// as a CSS variable.
    /// </summary>
    public required bool HasAttributeAlias { get; init; }

    /// <summary>
    /// The friendly HTML attribute name used on WC elements (e.g. <c>primary-color</c>);
    /// <see langword="null"/> when <see cref="HasAttributeAlias"/> is <see langword="false"/>.
    /// </summary>
    public string? AttributeAliasName { get; init; }

    /// <summary>
    /// The default value applied by the WC bundle before any tenant theme or host override.
    /// May be <see langword="null"/> when PLAN.md doesn't specify a concrete default.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>Value-shape classification driving the type-aware editor control.</summary>
    public required ThemeTokenValueType ValueType { get; init; }

    /// <summary>
    /// The WC (or WC family) the token is scoped to (e.g. <c>polar-mini-cart</c>);
    /// <see langword="null"/> for tenant-wide tokens that affect every WC.
    /// </summary>
    public string? ComponentScope { get; init; }
}
