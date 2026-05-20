using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Theming;

/// <summary>
/// The canonical registry of PolarSharp v1.4.0 design tokens that tenant theming
/// configures. Agent-consumable: agents constructing a tenant marketplace read
/// <see cref="AllTokens"/> for the full set, look up per-token metadata via the
/// <see cref="PolarThemeTokenDescriptor"/>, and generate the theme JSON the editor +
/// SignalR bootstrap consume.
/// </summary>
/// <remarks>
/// <para>
/// Categories (per PLAN.md v1.4.0 Phase 3): 12 colors + 6 typography + 3 spacing +
/// 3 shape + 2 motion + 4 component-specific + 1 semantic size-scale = <b>31 tokens total</b>.
/// 8 of the 31 carry a friendly HTML attribute alias (queryable via
/// <see cref="AttributeAliased"/>); the remaining 23 are CSS-variable-only (queryable via
/// <see cref="CssVariableOnly"/>).
/// </para>
/// <para>
/// Token names are exposed as <c>const string</c> members so agents and host code can
/// reference them via compile-checked identifiers (<c>PolarThemeTokens.Primary</c>)
/// rather than string literals.
/// </para>
/// <para>
/// Example: enumerate every color token that ships an attribute alias.
/// <code>
/// var aliasedColors = PolarThemeTokens.AttributeAliased
///     .Where(t => t.Category == ThemeTokenCategory.Colors);
/// foreach (var token in aliasedColors)
/// {
///     Console.WriteLine($"{token.Name} → attribute {token.AttributeAliasName}");
/// }
/// </code>
/// </para>
/// </remarks>
public static class PolarThemeTokens
{
    // ─── Colors (12) ─────────────────────────────────────────────────────────────

    /// <summary>Main brand color: CTA buttons, links, focus rings, selected highlights.</summary>
    public const string Primary = "--polar-primary";

    /// <summary>Secondary brand: category badges, decorative ribbons, secondary CTAs, hover highlights.</summary>
    public const string Accent = "--polar-accent";

    /// <summary>Card / panel / modal background. Tenant-wide.</summary>
    public const string Surface = "--polar-surface";

    /// <summary>Hover / active / pressed state for cards + buttons. Derived from surface.</summary>
    public const string SurfaceElevated = "--polar-surface-elevated";

    /// <summary>Body text color. Tenant-wide.</summary>
    public const string Text = "--polar-text";

    /// <summary>Secondary text: timestamps, helper text, strikethrough prices, breadcrumb separators.</summary>
    public const string TextMuted = "--polar-text-muted";

    /// <summary>Text on dark backgrounds: CTA button labels, badge text on primary/accent.</summary>
    public const string TextInverse = "--polar-text-inverse";

    /// <summary>Default borders: cards, inputs, dividers. Per-element override common.</summary>
    public const string Border = "--polar-border";

    /// <summary>Confirmation states: success toasts, checkmark icons, in-stock badges.</summary>
    public const string Success = "--polar-success";

    /// <summary>Warning states: low-stock badges, sale-ending banners.</summary>
    public const string Warning = "--polar-warning";

    /// <summary>Error states: declined-payment toasts, form-field validation errors, destructive buttons.</summary>
    public const string Error = "--polar-error";

    /// <summary>Informational states: shipment-status toasts, helper-tip backgrounds.</summary>
    public const string Info = "--polar-info";

    // ─── Typography (6) ──────────────────────────────────────────────────────────

    /// <summary>Primary font stack.</summary>
    public const string FontFamily = "--polar-font-family";

    /// <summary>Distinct heading font (optional; falls back to <see cref="FontFamily"/>).</summary>
    public const string FontFamilyHeading = "--polar-font-family-heading";

    /// <summary>Monospace for SKUs, license keys, order numbers, API tokens.</summary>
    public const string FontFamilyMono = "--polar-font-family-mono";

    /// <summary>Base font size (default 16px); every other size derives.</summary>
    public const string FontSizeBase = "--polar-font-size-base";

    /// <summary>What "bold" means for the tenant's chosen font (600 vs 700 etc.).</summary>
    public const string FontWeightBold = "--polar-font-weight-bold";

    /// <summary>Default line height (default 1.5).</summary>
    public const string LineHeight = "--polar-line-height";

    // ─── Spacing (3) ─────────────────────────────────────────────────────────────

    /// <summary>Base spacing unit (default 0.5rem = 8px). All paddings + margins + gaps derive.</summary>
    public const string SpacingUnit = "--polar-spacing-unit";

    /// <summary>Tight variant (default = 0.5× <see cref="SpacingUnit"/>).</summary>
    public const string SpacingTight = "--polar-spacing-tight";

    /// <summary>Loose variant (default = 2× <see cref="SpacingUnit"/>).</summary>
    public const string SpacingLoose = "--polar-spacing-loose";

    // ─── Shape (3) ───────────────────────────────────────────────────────────────

    /// <summary>Global corner radius for cards / buttons / inputs / badges.</summary>
    public const string BorderRadius = "--polar-border-radius";

    /// <summary>Default border thickness (default 1px).</summary>
    public const string BorderWidth = "--polar-border-width";

    /// <summary>Drop shadow intensity, semantic enum <c>none|sm|md|lg|xl</c>.</summary>
    public const string ShadowDepth = "--polar-shadow-depth";

    // ─── Motion (2) ──────────────────────────────────────────────────────────────

    /// <summary>Base animation duration (default 200ms).</summary>
    public const string AnimationDuration = "--polar-animation-duration";

    /// <summary>Easing function (default <c>cubic-bezier(0.4, 0, 0.2, 1)</c>).</summary>
    public const string AnimationEasing = "--polar-animation-easing";

    // ─── Component-specific (4) ──────────────────────────────────────────────────

    /// <summary>Image aspect ratio for product cards / grid / detail.</summary>
    public const string ProductCardImageAspectRatio = "--polar-product-card-image-aspect-ratio";

    /// <summary>Where the mini-cart docks (top-right default).</summary>
    public const string MiniCartPosition = "--polar-mini-cart-position";

    /// <summary>Where toasts stack.</summary>
    public const string ToastPosition = "--polar-toast-position";

    /// <summary>Default auto-dismiss duration (5000ms default).</summary>
    public const string ToastDuration = "--polar-toast-duration";

    // ─── Semantic size-scale (1) ─────────────────────────────────────────────────

    /// <summary>
    /// Maps the semantic <c>size="..."</c> attribute pattern (small/default/large/hero)
    /// to numeric multipliers. Default: <c>small=0.85, default=1.0, large=1.15, hero=1.35</c>.
    /// </summary>
    public const string SizeScale = "--polar-size-scale";

    // ─── Registry ────────────────────────────────────────────────────────────────

    private static readonly PolarThemeTokenDescriptor[] DescriptorsArray =
    {
        // Colors
        new() { Name = Primary, Category = ThemeTokenCategory.Colors, Description = "Main brand color: CTA buttons, links, focus rings, selected highlights.", HasAttributeAlias = true, AttributeAliasName = "primary-color", DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },
        new() { Name = Accent, Category = ThemeTokenCategory.Colors, Description = "Secondary brand: category badges, decorative ribbons, secondary CTAs, hover highlights.", HasAttributeAlias = true, AttributeAliasName = "accent-color", DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },
        new() { Name = Surface, Category = ThemeTokenCategory.Colors, Description = "Card / panel / modal background. Tenant-wide.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },
        new() { Name = SurfaceElevated, Category = ThemeTokenCategory.Colors, Description = "Hover / active / pressed state for cards + buttons. Derived from surface.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },
        new() { Name = Text, Category = ThemeTokenCategory.Colors, Description = "Body text color. Tenant-wide.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },
        new() { Name = TextMuted, Category = ThemeTokenCategory.Colors, Description = "Secondary text: timestamps, helper text, strikethrough prices, breadcrumb separators.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },
        new() { Name = TextInverse, Category = ThemeTokenCategory.Colors, Description = "Text on dark backgrounds: CTA button labels, badge text on primary/accent.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },
        new() { Name = Border, Category = ThemeTokenCategory.Colors, Description = "Default borders: cards, inputs, dividers. Per-element override common.", HasAttributeAlias = true, AttributeAliasName = "border-color", DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },
        new() { Name = Success, Category = ThemeTokenCategory.Colors, Description = "Confirmation states: success toasts, checkmark icons, in-stock badges.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },
        new() { Name = Warning, Category = ThemeTokenCategory.Colors, Description = "Warning states: low-stock badges, sale-ending banners.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },
        new() { Name = Error, Category = ThemeTokenCategory.Colors, Description = "Error states: declined-payment toasts, form-field validation errors, destructive buttons.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },
        new() { Name = Info, Category = ThemeTokenCategory.Colors, Description = "Informational states: shipment-status toasts, helper-tip backgrounds.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.Color, ComponentScope = null },

        // Typography
        new() { Name = FontFamily, Category = ThemeTokenCategory.Typography, Description = "Primary font stack.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.FontFamily, ComponentScope = null },
        new() { Name = FontFamilyHeading, Category = ThemeTokenCategory.Typography, Description = "Distinct heading font (optional; falls back to --polar-font-family).", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.FontFamily, ComponentScope = null },
        new() { Name = FontFamilyMono, Category = ThemeTokenCategory.Typography, Description = "Monospace for SKUs, license keys, order numbers, API tokens.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = null, ValueType = ThemeTokenValueType.FontFamily, ComponentScope = null },
        new() { Name = FontSizeBase, Category = ThemeTokenCategory.Typography, Description = "Base font size (default 16px); every other size derives.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = "16px", ValueType = ThemeTokenValueType.Numeric, ComponentScope = null },
        new() { Name = FontWeightBold, Category = ThemeTokenCategory.Typography, Description = "What 'bold' means for the tenant's chosen font (600 vs 700 etc.).", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = "700", ValueType = ThemeTokenValueType.Numeric, ComponentScope = null },
        new() { Name = LineHeight, Category = ThemeTokenCategory.Typography, Description = "Default line height (default 1.5).", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = "1.5", ValueType = ThemeTokenValueType.Numeric, ComponentScope = null },

        // Spacing
        new() { Name = SpacingUnit, Category = ThemeTokenCategory.Spacing, Description = "Base spacing unit (default 0.5rem = 8px). All paddings + margins + gaps derive.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = "0.5rem", ValueType = ThemeTokenValueType.Numeric, ComponentScope = null },
        new() { Name = SpacingTight, Category = ThemeTokenCategory.Spacing, Description = "Tight variant (default = 0.5× spacing-unit).", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = "0.25rem", ValueType = ThemeTokenValueType.Numeric, ComponentScope = null },
        new() { Name = SpacingLoose, Category = ThemeTokenCategory.Spacing, Description = "Loose variant (default = 2× spacing-unit).", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = "1rem", ValueType = ThemeTokenValueType.Numeric, ComponentScope = null },

        // Shape
        new() { Name = BorderRadius, Category = ThemeTokenCategory.Shape, Description = "Global corner radius for cards / buttons / inputs / badges.", HasAttributeAlias = true, AttributeAliasName = "border-radius", DefaultValue = null, ValueType = ThemeTokenValueType.Numeric, ComponentScope = null },
        new() { Name = BorderWidth, Category = ThemeTokenCategory.Shape, Description = "Default border thickness (default 1px).", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = "1px", ValueType = ThemeTokenValueType.Numeric, ComponentScope = null },
        new() { Name = ShadowDepth, Category = ThemeTokenCategory.Shape, Description = "Drop shadow intensity, semantic enum none|sm|md|lg|xl mapped to box-shadow values internally.", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = "md", ValueType = ThemeTokenValueType.Enum, ComponentScope = null },

        // Motion
        new() { Name = AnimationDuration, Category = ThemeTokenCategory.Motion, Description = "Base duration (default 200ms).", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = "200ms", ValueType = ThemeTokenValueType.Duration, ComponentScope = null },
        new() { Name = AnimationEasing, Category = ThemeTokenCategory.Motion, Description = "Easing function (default cubic-bezier(0.4, 0, 0.2, 1) = Material standard ease).", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = "cubic-bezier(0.4, 0, 0.2, 1)", ValueType = ThemeTokenValueType.Easing, ComponentScope = null },

        // Component-specific
        new() { Name = ProductCardImageAspectRatio, Category = ThemeTokenCategory.ComponentSpecific, Description = "Per-product image aspect ratio (fashion 3:4, electronics 1:1, lifestyle 16:9).", HasAttributeAlias = true, AttributeAliasName = "image-aspect-ratio", DefaultValue = "1:1", ValueType = ThemeTokenValueType.Ratio, ComponentScope = "polar-product-card" },
        new() { Name = MiniCartPosition, Category = ThemeTokenCategory.ComponentSpecific, Description = "Where the mini-cart docks (top-right default).", HasAttributeAlias = true, AttributeAliasName = "position", DefaultValue = "top-right", ValueType = ThemeTokenValueType.Position, ComponentScope = "polar-mini-cart" },
        new() { Name = ToastPosition, Category = ThemeTokenCategory.ComponentSpecific, Description = "Where toasts stack.", HasAttributeAlias = true, AttributeAliasName = "position", DefaultValue = "top-right", ValueType = ThemeTokenValueType.Position, ComponentScope = "polar-toast-host" },
        new() { Name = ToastDuration, Category = ThemeTokenCategory.ComponentSpecific, Description = "Default auto-dismiss duration (5000ms default); individual toasts override per-dispatcher payload.", HasAttributeAlias = true, AttributeAliasName = "toast-duration", DefaultValue = "5000ms", ValueType = ThemeTokenValueType.Duration, ComponentScope = "polar-toast-host" },

        // Semantic size-scale
        new() { Name = SizeScale, Category = ThemeTokenCategory.ComponentSpecific, Description = "Maps semantic size values (small/default/large/hero) to numeric multipliers (0.85/1.0/1.15/1.35 default).", HasAttributeAlias = false, AttributeAliasName = null, DefaultValue = "small=0.85, default=1.0, large=1.15, hero=1.35", ValueType = ThemeTokenValueType.Enum, ComponentScope = null },
    };

    /// <summary>The full token registry keyed by CSS variable name.</summary>
    public static IReadOnlyDictionary<string, PolarThemeTokenDescriptor> AllTokens { get; } =
        new ReadOnlyDictionary<string, PolarThemeTokenDescriptor>(
            DescriptorsArray.ToDictionary(d => d.Name));

    /// <summary>The 8 tokens that expose a friendly HTML attribute alias on WC elements.</summary>
    public static IReadOnlyList<PolarThemeTokenDescriptor> AttributeAliased { get; } =
        DescriptorsArray.Where(d => d.HasAttributeAlias).ToArray();

    /// <summary>The 23 tokens that are CSS-variable-only (no attribute alias).</summary>
    public static IReadOnlyList<PolarThemeTokenDescriptor> CssVariableOnly { get; } =
        DescriptorsArray.Where(d => !d.HasAttributeAlias).ToArray();

    /// <summary>
    /// Returns the tokens that belong to the supplied <paramref name="category"/>.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>A read-only list of descriptors matching the category.</returns>
    /// <example>
    /// <code>
    /// var colorTokens = PolarThemeTokens.ByCategory(ThemeTokenCategory.Colors);
    /// // → 12 entries
    /// </code>
    /// </example>
    public static IReadOnlyList<PolarThemeTokenDescriptor> ByCategory(ThemeTokenCategory category) =>
        DescriptorsArray.Where(d => d.Category == category).ToArray();
}
