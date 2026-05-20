namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// Target WCAG conformance level for a PolarSharp Web Component. The catalog baseline
/// is <see cref="AA"/> (committed in PLAN.md as the launch floor); individual WCs may
/// target a higher level when accessibility is a primary feature.
/// </summary>
public enum WcagComplianceLevel
{
    /// <summary>WCAG 2.2 Level A — minimal conformance.</summary>
    A = 0,

    /// <summary>WCAG 2.2 Level AA — PolarSharp launch baseline.</summary>
    AA = 1,

    /// <summary>WCAG 2.2 Level AAA — highest conformance level.</summary>
    AAA = 2,
}
