namespace PolarSharp.EcommerceStorefronts.Abstractions.Theming;

/// <summary>
/// Value-shape classification for a PolarSharp design token. Drives the type-aware
/// editor control (color picker vs slider vs dropdown vs position grid, etc.) and the
/// JSON-import validator that runs before a theme is committed.
/// </summary>
public enum ThemeTokenValueType
{
    /// <summary>Color value — hex, rgb(), hsl(), or named color.</summary>
    Color = 0,

    /// <summary>Numeric value — px, em, rem, or unitless multiplier.</summary>
    Numeric = 1,

    /// <summary>Discrete enum value (e.g. shadow-depth = none|sm|md|lg|xl).</summary>
    Enum = 2,

    /// <summary>Font stack / font family string.</summary>
    FontFamily = 3,

    /// <summary>Position value (top-left / top-center / top-right / ... / bottom-right).</summary>
    Position = 4,

    /// <summary>Animation duration — milliseconds or seconds.</summary>
    Duration = 5,

    /// <summary>CSS easing function string (cubic-bezier, ease, ease-in-out, etc.).</summary>
    Easing = 6,

    /// <summary>Aspect ratio (e.g. 1:1 / 4:3 / 16:9 / 3:4) for image and media tokens.</summary>
    Ratio = 7,
}
