namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// Accessibility metadata for a PolarSharp Web Component. Mirrors the <c>accessibility:</c>
/// block in the per-WC YAML frontmatter spec from PLAN.md so agents can mechanically check
/// a WC's accessibility posture against a tenant's
/// <c>TenantAccessibilityPolicy</c> before including it in a generated marketplace.
/// </summary>
public sealed record WcAccessibility
{
    /// <summary>The WCAG 2.2 conformance level the WC targets.</summary>
    public required WcagComplianceLevel WcagLevel { get; init; }

    /// <summary>
    /// Plain-language description of how WC state is conveyed without relying on color
    /// alone (e.g. "stock-badge always paired with text label"). Drives the
    /// color-blindness review during accessibility validation.
    /// </summary>
    public required string ColorNonConveyancePattern { get; init; }

    /// <summary>
    /// <see langword="true"/> when every interactive element is reachable via keyboard
    /// (Tab / Shift+Tab / arrow keys per the standard WAI-ARIA patterns).
    /// </summary>
    public required bool KeyboardReachable { get; init; }

    /// <summary>
    /// <see langword="true"/> when the WC has been validated against a screen reader
    /// (NVDA on Windows, VoiceOver on macOS/iOS, TalkBack on Android).
    /// </summary>
    public required bool ScreenReaderTested { get; init; }
}
