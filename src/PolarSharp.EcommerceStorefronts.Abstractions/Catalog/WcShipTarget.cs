namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// Release-window assignment for a PolarSharp Web Component. Drives release-notes generation,
/// CHANGELOG entries, and agent-side decisions about whether a WC is available "today" for a
/// new marketplace or scheduled for a later patch.
/// </summary>
public enum WcShipTarget
{
    /// <summary>Ships in v1.4.0 with the initial WC catalog release.</summary>
    V140 = 0,

    /// <summary>Ships in v1.4.1 (currently planned: SSO GitHub + LinkedIn).</summary>
    V141 = 1,

    /// <summary>Ships in v1.4.2 (currently planned: SSO X / Snapchat / Pinterest / TikTok).</summary>
    V142 = 2,

    /// <summary>Scheduled for a v1.4.x patch but no named patch version yet (cross-sell, popups, pre-order, waitlist, charity-donation, coupon popup, exit-intent).</summary>
    V14xUnscheduled = 3,

    /// <summary>Ships in v1.5+ (impact-statement, build-your-own-bundle, gift-finder-wizard, tip-jar, influencer-storefront, live-shopping-event, product-360-viewer 3D-mode).</summary>
    V15Plus = 4,

    /// <summary>Deferred to a dedicated planning session; not yet committed to any release (comparison tables).</summary>
    DeferredToPlanning = 5,
}
