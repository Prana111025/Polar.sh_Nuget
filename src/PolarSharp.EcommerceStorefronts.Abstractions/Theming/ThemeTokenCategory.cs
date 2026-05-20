namespace PolarSharp.EcommerceStorefronts.Abstractions.Theming;

/// <summary>
/// Functional category for a PolarSharp design token. Matches the grouping used by the
/// tenant theme editor and PLAN.md's v1.4.0 Phase 3 token catalog so the editor can render
/// per-category panels and agents can filter by category when generating themes.
/// </summary>
public enum ThemeTokenCategory
{
    /// <summary>Color tokens (primary, accent, surface, text, semantic states, etc.).</summary>
    Colors = 0,

    /// <summary>Font family, font size, font weight, line height tokens.</summary>
    Typography = 1,

    /// <summary>Spacing units used for paddings, margins, and gaps.</summary>
    Spacing = 2,

    /// <summary>Border-radius, border-width, and shadow-depth tokens.</summary>
    Shape = 3,

    /// <summary>Animation duration and easing tokens.</summary>
    Motion = 4,

    /// <summary>
    /// Tokens scoped to a single Web Component or small WC family (e.g. mini-cart position,
    /// toast position/duration, product-card image aspect ratio, semantic size-scale mapping).
    /// </summary>
    ComponentSpecific = 5,
}
