using System.Collections.Generic;
using System.Linq;
using PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Tests.Catalog;

/// <summary>
/// Contract tests for <see cref="WcCatalog"/>. Guards the WC count, naming prefix,
/// ship-target tally, and cross-reference integrity (composes-with / conflicts-with /
/// emits/listens event pairing) so accidental edits to the descriptor list fail loudly
/// instead of silently shifting the agent-consumable catalog contract.
/// </summary>
public sealed class WcCatalogTests
{
    [Fact]
    public void AllWcs_count_matches_PLAN_md_tally()
    {
        // PLAN.md "WC catalog + agent-readable frontmatter spec":
        //   18 baseline + 30 storefront + 13 reporting + 24 merchandising = 85
        // + 2 deferred-to-planning (comparison tables) = 87 entries total.
        Assert.Equal(87, WcCatalog.AllWcs.Count);
    }

    [Fact]
    public void Ship_target_breakdown_matches_PLAN_md_distribution()
    {
        var v140 = WcCatalog.ByShipTarget(WcShipTarget.V140).Count;
        var v141 = WcCatalog.ByShipTarget(WcShipTarget.V141).Count;
        var v142 = WcCatalog.ByShipTarget(WcShipTarget.V142).Count;
        var v14x = WcCatalog.ByShipTarget(WcShipTarget.V14xUnscheduled).Count;
        var v15Plus = WcCatalog.ByShipTarget(WcShipTarget.V15Plus).Count;
        var deferred = WcCatalog.ByShipTarget(WcShipTarget.DeferredToPlanning).Count;

        // PLAN.md headline: "~69 WCs ship in v1.4.0". Our enumeration locks the exact figure
        // (slightly higher than PLAN's headline because the catalog assigns the base
        // polar-product-360-viewer to v1.4.0 and uses ShipTargetNote to flag the v1.5+
        // 3D-model mode rather than splitting it into two distinct WCs).
        Assert.Equal(73, v140);

        // v1.4.x patches: cross-sell, coupon-popup, exit-intent, pre-order, waitlist, charity-donation.
        Assert.Equal(6, v14x);

        // v1.5+: impact-statement, build-your-own-bundle, gift-finder-wizard, tip-jar,
        // influencer-storefront, live-shopping-event. (Product-360-viewer 3D mode is split-target;
        // we encode the base WC as V140 with a ShipTargetNote for the v1.5+ 3D mode.)
        Assert.Equal(6, v15Plus);

        // Deferred to dedicated planning: product + plan comparison tables.
        Assert.Equal(2, deferred);

        // v1.4.1 + v1.4.2 lanes are reserved for SSO provider packages, not WCs themselves —
        // the SSO WC ships in v1.4.0 and renders whatever providers are configured.
        Assert.Equal(0, v141);
        Assert.Equal(0, v142);

        Assert.Equal(WcCatalog.AllWcs.Count, v140 + v141 + v142 + v14x + v15Plus + deferred);
    }

    [Fact]
    public void Every_wc_name_starts_with_polar_prefix()
    {
        foreach (var (name, descriptor) in WcCatalog.AllWcs)
        {
            Assert.StartsWith("polar-", name);
            Assert.Equal(name, descriptor.Name);
        }
    }

    [Fact]
    public void Wc_names_are_unique()
    {
        var distinct = WcCatalog.AllWcs.Keys.Distinct().Count();
        Assert.Equal(WcCatalog.AllWcs.Count, distinct);
    }

    [Fact]
    public void Every_composes_with_reference_resolves_to_an_existing_wc()
    {
        var registry = WcCatalog.AllWcs;
        var failures = new List<string>();

        foreach (var (name, descriptor) in registry)
        {
            foreach (var compose in descriptor.ComposesWith)
            {
                if (!registry.ContainsKey(compose))
                {
                    failures.Add($"{name}.ComposesWith references unknown WC '{compose}'");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    [Fact]
    public void Every_conflicts_with_reference_resolves_to_an_existing_wc()
    {
        var registry = WcCatalog.AllWcs;
        var failures = new List<string>();

        foreach (var (name, descriptor) in registry)
        {
            foreach (var conflict in descriptor.ConflictsWith)
            {
                if (!registry.ContainsKey(conflict))
                {
                    failures.Add($"{name}.ConflictsWith references unknown WC '{conflict}'");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    /// <summary>
    /// Events the PolarSharp backend pushes over the SignalR hub (no WC emits them — they
    /// originate server-side from webhooks, inventory updates, shipment carrier callbacks,
    /// and the refund pipeline). WCs may legitimately listen to these without there being
    /// a peer emitter in the WC catalog.
    /// </summary>
    private static readonly HashSet<string> ServerPushedEvents = new()
    {
        "polarToastDispatched",
        "polarInventoryChanged",
        "polarShipmentStatusChanged",
        "polarRefundStatusChanged",
    };

    [Fact]
    public void Every_internal_listens_to_event_has_at_least_one_emitter_or_is_server_pushed()
    {
        // Polar-internal events (prefixed `polar`) should either be emitted by at least one
        // other WC OR be on the documented server-pushed-via-SignalR allow-list. Standard DOM
        // events / arbitrary host events are skipped entirely.
        var emittedEvents = new HashSet<string>(
            WcCatalog.AllWcs.Values.SelectMany(d => d.EmitsEvents));

        var failures = new List<string>();

        foreach (var (name, descriptor) in WcCatalog.AllWcs)
        {
            foreach (var listened in descriptor.ListensToEvents)
            {
                // Skip non-PolarSharp events (e.g. DOM 'click', host-app 'app:ready') — only
                // gate Polar-internal events.
                if (!listened.StartsWith("polar"))
                {
                    continue;
                }

                if (ServerPushedEvents.Contains(listened))
                {
                    continue;
                }

                if (!emittedEvents.Contains(listened))
                {
                    failures.Add($"{name} listens to '{listened}' but no WC emits it and it is not on the server-pushed allow-list");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    [Fact]
    public void Section_counts_match_PLAN_md_grouping()
    {
        // The catalog organises WCs by section in PLAN.md. We can't read the section
        // membership from the descriptor (intentionally — descriptors don't carry section
        // metadata), but the totals should still satisfy the documented breakdown.
        var all = WcCatalog.AllWcs.Count;
        Assert.Equal(87, all); // 18 + 30 + 13 + 24 + 2
    }

    [Fact]
    public void Embed_anywhere_only_wcs_are_anonymous_safe_or_explicitly_authenticated_aware()
    {
        // Sanity invariant: every WC that ships outside the tenant's own storefront should
        // accept anonymous users — that's the whole point of the embed-anywhere split.
        foreach (var descriptor in WcCatalog.AllWcs.Values
            .Where(d => d.Deployment == WcDeploymentContext.EmbedAnywhere))
        {
            Assert.NotEqual(WcAudience.AuthenticatedRequired, descriptor.Audience);
        }
    }

    [Fact]
    public void Tenant_storefront_only_wcs_never_require_partner_site_compatibility()
    {
        // Spot-check the known tenant-storefront WCs are correctly classified.
        Assert.Equal(WcDeploymentContext.TenantStorefrontOnly, WcCatalog.AllWcs[WcCatalog.PolarAccountMenu].Deployment);
        Assert.Equal(WcDeploymentContext.TenantStorefrontOnly, WcCatalog.AllWcs[WcCatalog.PolarOrderHistoryList].Deployment);
        Assert.Equal(WcDeploymentContext.TenantStorefrontOnly, WcCatalog.AllWcs[WcCatalog.PolarWalletBalance].Deployment);
        Assert.Equal(WcDeploymentContext.TenantStorefrontOnly, WcCatalog.AllWcs[WcCatalog.PolarSubscriptionList].Deployment);
    }

    [Fact]
    public void ByDecisionTreeTag_returns_relevant_subset()
    {
        var subscriptionWcs = WcCatalog.ByDecisionTreeTag("subscriptions");
        Assert.NotEmpty(subscriptionWcs);
        Assert.Contains(subscriptionWcs, d => d.Name == WcCatalog.PolarPlanPicker);
        Assert.Contains(subscriptionWcs, d => d.Name == WcCatalog.PolarSubscriptionList);

        var b2bWcs = WcCatalog.ByDecisionTreeTag("b2b");
        Assert.NotEmpty(b2bWcs);
    }

    [Fact]
    public void Split_ship_target_wcs_carry_a_ship_target_note()
    {
        // polar-product-360-viewer has dual delivery (spin-frames v1.4.0; 3D-model v1.5+).
        var viewer = WcCatalog.AllWcs[WcCatalog.PolarProduct360Viewer];
        Assert.NotNull(viewer.ShipTargetNote);
    }

    [Fact]
    public void Accessibility_metadata_is_present_on_every_wc()
    {
        foreach (var (name, descriptor) in WcCatalog.AllWcs)
        {
            Assert.NotNull(descriptor.Accessibility);
            Assert.False(
                string.IsNullOrWhiteSpace(descriptor.Accessibility.ColorNonConveyancePattern),
                $"{name} has empty accessibility ColorNonConveyancePattern");
        }
    }
}
