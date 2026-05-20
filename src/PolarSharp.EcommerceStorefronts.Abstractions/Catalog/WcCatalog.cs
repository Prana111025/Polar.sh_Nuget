using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// The canonical registry of PolarSharp v1.4.0 Phase 3 Web Components. Agent-consumable:
/// agents constructing a tenant marketplace read <see cref="AllWcs"/> for the full set
/// and filter via <see cref="ByShipTarget(WcShipTarget)"/>,
/// <see cref="ByDeployment(WcDeploymentContext)"/>, and
/// <see cref="ByDecisionTreeTag(string)"/> when selecting a per-niche WC subset.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors PLAN.md's "WC catalog + agent-readable frontmatter spec" sub-section. WC names
/// are exposed as <c>const string</c> members so consuming code references them via
/// compile-checked identifiers (e.g. <c>WcCatalog.PolarProductCard</c>) rather than string
/// literals.
/// </para>
/// <para>
/// Distribution across release windows (per PLAN.md): the bulk ship in v1.4.0 with the
/// initial bundle release, a smaller set of merchandising / SSO additions ship as
/// v1.4.x patches, the most-experimental items ship in v1.5+, and two comparison-table WCs
/// are deferred to a dedicated planning session.
/// </para>
/// </remarks>
public static class WcCatalog
{
    // ─── Baseline (18) ───────────────────────────────────────────────────────────

    /// <summary>Single product display.</summary>
    public const string PolarProductCard = "polar-product-card";

    /// <summary>Grid of product cards.</summary>
    public const string PolarProductGrid = "polar-product-grid";

    /// <summary>Product detail page.</summary>
    public const string PolarProductDetail = "polar-product-detail";

    /// <summary>Mini cart dock icon + popover.</summary>
    public const string PolarMiniCart = "polar-mini-cart";

    /// <summary>Slide-in cart drawer.</summary>
    public const string PolarCartDrawer = "polar-cart-drawer";

    /// <summary>Buy-now CTA (skips cart).</summary>
    public const string PolarCheckoutButton = "polar-checkout-button";

    /// <summary>Inline auth + multi-step checkout flow.</summary>
    public const string PolarCheckoutPage = "polar-checkout-page";

    /// <summary>Unified sign-in / account widget (replaces login/logout pair).</summary>
    public const string PolarAccountMenu = "polar-account-menu";

    /// <summary>Customer's past orders.</summary>
    public const string PolarOrderHistoryList = "polar-order-history-list";

    /// <summary>Customer's wallet balance display.</summary>
    public const string PolarWalletBalance = "polar-wallet-balance";

    /// <summary>Add funds to wallet.</summary>
    public const string PolarWalletTopupFlow = "polar-wallet-topup-flow";

    /// <summary>Razor TagHelper bootstrap (MVC convenience).</summary>
    public const string PolarStorefrontScript = "polar-storefront-script";

    /// <summary>Toast notification renderer (SignalR-subscribed).</summary>
    public const string PolarToastHost = "polar-toast-host";

    /// <summary>Search input + autosuggest dropdown.</summary>
    public const string PolarProductSearch = "polar-product-search";

    /// <summary>Faceted filter sidebar.</summary>
    public const string PolarProductFilters = "polar-product-filters";

    /// <summary>Customer's saved billing/shipping addresses.</summary>
    public const string PolarSavedAddresses = "polar-saved-addresses";

    /// <summary>Customer's saved cards.</summary>
    public const string PolarSavedPaymentMethods = "polar-saved-payment-methods";

    /// <summary>Customer's active subscriptions.</summary>
    public const string PolarSubscriptionList = "polar-subscription-list";

    // ─── Storefront additions (30) ───────────────────────────────────────────────

    /// <summary>Visual tile for one category.</summary>
    public const string PolarCategoryTile = "polar-category-tile";

    /// <summary>Grid of category tiles.</summary>
    public const string PolarCategoryGrid = "polar-category-grid";

    /// <summary>Hierarchical breadcrumb navigation.</summary>
    public const string PolarBreadcrumbTrail = "polar-breadcrumb-trail";

    /// <summary>Mega-menu of departments + categories.</summary>
    public const string PolarDepartmentMenu = "polar-department-menu";

    /// <summary>Color/size/material picker; emits <c>polarVariantChanged</c>.</summary>
    public const string PolarVariantSelector = "polar-variant-selector";

    /// <summary>+/- quantity input.</summary>
    public const string PolarQuantityStepper = "polar-quantity-stepper";

    /// <summary>Standalone add-to-cart (vs buy-now).</summary>
    public const string PolarAddToCartButton = "polar-add-to-cart-button";

    /// <summary>Inventory badge (in-stock / low / out-of-stock).</summary>
    public const string PolarStockIndicator = "polar-stock-indicator";

    /// <summary>Full <c>/cart</c> page line-items list.</summary>
    public const string PolarCartSummary = "polar-cart-summary";

    /// <summary>Discount code entry.</summary>
    public const string PolarPromoCodeInput = "polar-promo-code-input";

    /// <summary>ZIP-based shipping rate preview.</summary>
    public const string PolarShippingEstimator = "polar-shipping-estimator";

    /// <summary>Billing/shipping input + autocomplete.</summary>
    public const string PolarAddressForm = "polar-address-form";

    /// <summary>CC / wallet / saved card selection.</summary>
    public const string PolarPaymentMethodPicker = "polar-payment-method-picker";

    /// <summary>2-3 card pricing picker with selection state.</summary>
    public const string PolarPlanPicker = "polar-plan-picker";

    /// <summary>Amazon-style image+video gallery.</summary>
    public const string PolarProductMediaGallery = "polar-product-media-gallery";

    /// <summary>TOTP + SMS 2FA enrollment UI.</summary>
    public const string Polar2faSetup = "polar-2fa-setup";

    /// <summary>2FA challenge during sign-in.</summary>
    public const string Polar2faChallenge = "polar-2fa-challenge";

    /// <summary>Passkey/WebAuthn enrollment UI.</summary>
    public const string PolarPasskeySetup = "polar-passkey-setup";

    /// <summary>Passkey challenge during sign-in.</summary>
    public const string PolarPasskeyChallenge = "polar-passkey-challenge";

    /// <summary>Full-page search results display.</summary>
    public const string PolarSearchResults = "polar-search-results";

    /// <summary>"Recently viewed" carousel (localStorage + cross-device sync).</summary>
    public const string PolarRecentlyViewed = "polar-recently-viewed";

    /// <summary>Editorial image with clickable product hotspots.</summary>
    public const string PolarShoppableImage = "polar-shoppable-image";

    /// <summary>Interactive 360-degree product photography (spin-frames v1.4.0; 3D-model v1.5+).</summary>
    public const string PolarProduct360Viewer = "polar-product-360-viewer";

    /// <summary>Interactive size guide for apparel.</summary>
    public const string PolarSizeGuide = "polar-size-guide";

    /// <summary>Customer Q&amp;A section.</summary>
    public const string PolarProductQuestions = "polar-product-questions";

    /// <summary>Average star rating + distribution chart.</summary>
    public const string PolarProductRating = "polar-product-rating";

    /// <summary>Full reviews list with filtering + helpful-voting.</summary>
    public const string PolarProductReviews = "polar-product-reviews";

    /// <summary>Write-a-review (verified purchasers only).</summary>
    public const string PolarProductReviewForm = "polar-product-review-form";

    /// <summary>AI-generated "What customers love / criticize" summary.</summary>
    public const string PolarProductAiSummary = "polar-product-ai-summary";

    /// <summary>Renders configured SSO providers' sign-in buttons.</summary>
    public const string PolarSsoButtonGroup = "polar-sso-button-group";

    // ─── Reporting additions (13) ────────────────────────────────────────────────

    /// <summary>Single-order drilldown.</summary>
    public const string PolarOrderDetail = "polar-order-detail";

    /// <summary>Shipment tracking display.</summary>
    public const string PolarOrderTracking = "polar-order-tracking";

    /// <summary>Invoice PDF download + print view.</summary>
    public const string PolarInvoiceViewer = "polar-invoice-viewer";

    /// <summary>Post-purchase confirmation receipt.</summary>
    public const string PolarReceipt = "polar-receipt";

    /// <summary>"You've spent $X this year" personalized stats.</summary>
    public const string PolarSpendSummary = "polar-spend-summary";

    /// <summary>Single-subscription drilldown (change/cancel).</summary>
    public const string PolarSubscriptionDetail = "polar-subscription-detail";

    /// <summary>Refund-request timeline.</summary>
    public const string PolarRefundStatusTracker = "polar-refund-status-tracker";

    /// <summary>Active customer benefits (license keys, downloads, Discord roles).</summary>
    public const string PolarBenefitList = "polar-benefit-list";

    /// <summary>Single license-key with copy + activation status.</summary>
    public const string PolarLicenseKeyDisplay = "polar-license-key-display";

    /// <summary>Customer's downloadable files.</summary>
    public const string PolarDownloadList = "polar-download-list";

    /// <summary>Loyalty tier + points + progress.</summary>
    public const string PolarLoyaltyStatus = "polar-loyalty-status";

    /// <summary>Referrals + rewards earned.</summary>
    public const string PolarReferralTracker = "polar-referral-tracker";

    /// <summary>Sustainability messaging ("planted 3 trees").</summary>
    public const string PolarImpactStatement = "polar-impact-statement";

    // ─── Merchandising additions (24) ────────────────────────────────────────────

    /// <summary>Site-wide promo bar.</summary>
    public const string PolarPromoBanner = "polar-promo-banner";

    /// <summary>Sale-ending countdown ticker.</summary>
    public const string PolarCountdownTimer = "polar-countdown-timer";

    /// <summary>Time-limited product card with countdown.</summary>
    public const string PolarFlashSaleCard = "polar-flash-sale-card";

    /// <summary>Payment + security + guarantee badges.</summary>
    public const string PolarTrustBadges = "polar-trust-badges";

    /// <summary>"Featured items for {category}" carousel.</summary>
    public const string PolarFeaturedCollection = "polar-featured-collection";

    /// <summary>Manually-curated product list.</summary>
    public const string PolarCuratedCollection = "polar-curated-collection";

    /// <summary>Themed seasonal collections.</summary>
    public const string PolarOccasionShop = "polar-occasion-shop";

    /// <summary>"Upgrade to Premium for $X more" inline upsell.</summary>
    public const string PolarUpSellCard = "polar-up-sell-card";

    /// <summary>"Buy 2 get 1 free" promo display.</summary>
    public const string PolarBuyXGetY = "polar-buy-x-get-y";

    /// <summary>"Save 20% on this bundle" display.</summary>
    public const string PolarBundleOffer = "polar-bundle-offer";

    /// <summary>Cart-aware tier progress ("$50 more for 10% off").</summary>
    public const string PolarDiscountTierBadge = "polar-discount-tier-badge";

    /// <summary>Newsletter signup with discount incentive.</summary>
    public const string PolarEmailCapture = "polar-email-capture";

    /// <summary>Social-proof ticker for recent purchases.</summary>
    public const string PolarRecentlyPurchasedByOthers = "polar-recently-purchased-by-others";

    /// <summary>"Customers also bought" grid.</summary>
    public const string PolarCrossSellGrid = "polar-cross-sell-grid";

    /// <summary>Triggered modal with discount code.</summary>
    public const string PolarCouponPopup = "polar-coupon-popup";

    /// <summary>Modal on cursor-leaves-viewport.</summary>
    public const string PolarExitIntentModal = "polar-exit-intent-modal";

    /// <summary>Pre-order product card with launch countdown.</summary>
    public const string PolarPreOrderCard = "polar-pre-order-card";

    /// <summary>"Notify me when back in stock" signup.</summary>
    public const string PolarWaitlistButton = "polar-waitlist-button";

    /// <summary>Cart-level charity donation.</summary>
    public const string PolarCharityDonationAddOn = "polar-charity-donation-add-on";

    /// <summary>Interactive bundle builder.</summary>
    public const string PolarBuildYourOwnBundle = "polar-build-your-own-bundle";

    /// <summary>AI-driven conversational gift recommender.</summary>
    public const string PolarGiftFinderWizard = "polar-gift-finder-wizard";

    /// <summary>Tip the maker/creator.</summary>
    public const string PolarTipJar = "polar-tip-jar";

    /// <summary>Curated micro-storefront by affiliate.</summary>
    public const string PolarInfluencerStorefront = "polar-influencer-storefront";

    /// <summary>Streaming live-shopping event with chat.</summary>
    public const string PolarLiveShoppingEvent = "polar-live-shopping-event";

    // ─── Deferred to dedicated planning (2) ──────────────────────────────────────

    /// <summary>Side-by-side product comparison.</summary>
    public const string PolarProductComparisonTable = "polar-product-comparison-table";

    /// <summary>Subscription plan feature-matrix comparison.</summary>
    public const string PolarPlanComparisonTable = "polar-plan-comparison-table";

    // ─── Reusable accessibility helpers ──────────────────────────────────────────

    private static readonly WcAccessibility DefaultA11y = new()
    {
        WcagLevel = WcagComplianceLevel.AA,
        ColorNonConveyancePattern = "all interactive elements paired with text labels or icons",
        KeyboardReachable = true,
        ScreenReaderTested = true,
    };

    private static WcAccessibility A11y(string colorPattern) => new()
    {
        WcagLevel = WcagComplianceLevel.AA,
        ColorNonConveyancePattern = colorPattern,
        KeyboardReachable = true,
        ScreenReaderTested = true,
    };

    private static readonly string[] Empty = Array.Empty<string>();

    private static readonly WcDescriptor[] DescriptorsArray =
    {
        // ─── Baseline (18) ───────────────────────────────────────────────────
        new()
        {
            Name = PolarProductCard,
            Purpose = "Display a single product with image, name, price, optional badges.",
            DataSource = "IStorefrontCatalogProvider.GetProductAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductGrid, PolarProductDetail, PolarAddToCartButton, PolarStockIndicator, PolarProductRating },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "catalog must have at least one published product" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "subscriptions", "b2c", "b2b" },
            EmitsEvents = new[] { "polarAddToCartTriggered" },
            ListensToEvents = new[] { "polarVariantChanged" },
            Accessibility = A11y("stock-badge always paired with text label"),
        },
        new()
        {
            Name = PolarProductGrid,
            Purpose = "Grid of product cards with pagination.",
            DataSource = "IStorefrontCatalogProvider.ListProductsAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductCard, PolarProductFilters, PolarBreadcrumbTrail },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "catalog must have at least one published product" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "subscriptions", "b2c", "b2b" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarFiltersChanged" },
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarProductDetail,
            Purpose = "Full product detail page with media gallery, description, variants, and add-to-cart.",
            DataSource = "IStorefrontCatalogProvider.GetProductAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductMediaGallery, PolarVariantSelector, PolarQuantityStepper, PolarAddToCartButton, PolarStockIndicator, PolarProductRating, PolarProductReviews, PolarProductQuestions, PolarProductAiSummary },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "catalog must have at least one published product" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "subscriptions", "b2c", "b2b" },
            EmitsEvents = new[] { "polarAddToCartTriggered" },
            ListensToEvents = new[] { "polarVariantChanged" },
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarMiniCart,
            Purpose = "Mini cart dock icon + popover showing line-items.",
            DataSource = "IStorefrontCartService.GetCartAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCartDrawer, PolarCheckoutButton },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "b2c" },
            EmitsEvents = new[] { "polarCartOpenRequested" },
            ListensToEvents = new[] { "polarAddToCartTriggered", "polarCartUpdated" },
            Accessibility = A11y("cart item count rendered as text + badge color"),
        },
        new()
        {
            Name = PolarCartDrawer,
            Purpose = "Slide-in cart drawer with editable line-items.",
            DataSource = "IStorefrontCartService.GetCartAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarMiniCart, PolarQuantityStepper, PolarPromoCodeInput, PolarCheckoutButton },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "b2c" },
            EmitsEvents = new[] { "polarCartUpdated", "polarCheckoutTriggered" },
            ListensToEvents = new[] { "polarCartOpenRequested" },
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarCheckoutButton,
            Purpose = "Buy-now CTA that skips the cart and goes straight to checkout.",
            DataSource = "IStorefrontCatalogProvider.GetProductAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductDetail, PolarProductCard },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "subscriptions", "b2c" },
            EmitsEvents = new[] { "polarCheckoutTriggered" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarCheckoutPage,
            Purpose = "Inline auth + multi-step checkout flow (5 steps; supports guest, password, 2FA, passkey, SSO).",
            DataSource = "IStorefrontCheckoutService.StartCheckoutAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarAddressForm, PolarPaymentMethodPicker, PolarShippingEstimator, PolarPromoCodeInput, Polar2faChallenge, PolarPasskeyChallenge, PolarSsoButtonGroup, PolarSavedAddresses, PolarSavedPaymentMethods },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "TenantSignupConfig published" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "subscriptions", "b2c", "b2b" },
            EmitsEvents = new[] { "polarOrderPlaced", "polarAccountCreated" },
            ListensToEvents = Empty,
            Accessibility = A11y("step indicator labels announce progress to screen readers; error states paired with text"),
        },
        new()
        {
            Name = PolarAccountMenu,
            Purpose = "Unified sign-in / account widget (replaces the dropped login + logout pair).",
            DataSource = "IStorefrontIdentityProvider.GetCurrentCustomerAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarOrderHistoryList, PolarSavedAddresses, PolarSubscriptionList, PolarWalletBalance },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "b2c", "b2b" },
            EmitsEvents = new[] { "polarSignInRequested", "polarSignOutTriggered" },
            ListensToEvents = new[] { "polarAccountCreated" },
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarOrderHistoryList,
            Purpose = "Customer's past orders with pagination + filtering.",
            DataSource = "IStorefrontCustomerService.ListOrdersAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarOrderDetail, PolarOrderTracking, PolarInvoiceViewer, PolarRefundStatusTracker },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "b2c", "b2b" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarWalletBalance,
            Purpose = "Customer's prepaid wallet balance display.",
            DataSource = "IStorefrontWalletService.GetBalanceAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarWalletTopupFlow, PolarAccountMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "PrepaidWallets feature enabled for tenant" },
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "wallet", "b2c" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarWalletTopupCompleted", "polarOrderPlaced" },
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarWalletTopupFlow,
            Purpose = "Add funds to wallet via configured payment methods.",
            DataSource = "IStorefrontWalletService.StartTopupAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarWalletBalance, PolarPaymentMethodPicker },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "PrepaidWallets feature enabled for tenant" },
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "wallet", "b2c" },
            EmitsEvents = new[] { "polarWalletTopupCompleted" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarStorefrontScript,
            Purpose = "Razor TagHelper bootstrap convenience for MVC hosts.",
            DataSource = "(host tenant config)",
            BackendDependencies = Empty,
            ComposesWith = Empty,
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "embed-key present" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "bootstrap" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarToastHost,
            Purpose = "Toast notification renderer subscribed to SignalR live updates.",
            DataSource = "(SignalR hub: tenant toast channel)",
            BackendDependencies = Empty,
            ComposesWith = Empty,
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "embed-key present" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "live-updates" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarToastDispatched" },
            Accessibility = A11y("aria-live region announces toasts to screen readers; auto-dismiss is paused on hover/focus"),
        },
        new()
        {
            Name = PolarProductSearch,
            Purpose = "Search input with autosuggest dropdown.",
            DataSource = "IStorefrontSearchProvider.SuggestAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarSearchResults, PolarProductFilters },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "search index populated" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "search", "physical-goods", "digital-goods" },
            EmitsEvents = new[] { "polarSearchSubmitted" },
            ListensToEvents = Empty,
            Accessibility = A11y("aria-autocomplete + role=combobox per ARIA pattern"),
        },
        new()
        {
            Name = PolarProductFilters,
            Purpose = "Faceted filter sidebar (categories, price range, brand, attributes).",
            DataSource = "IStorefrontCatalogProvider.GetFacetsAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductGrid, PolarSearchResults },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "catalog must have published products with facets" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = new[] { "polarFiltersChanged" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarSavedAddresses,
            Purpose = "Customer's saved billing/shipping addresses.",
            DataSource = "IStorefrontCustomerService.ListAddressesAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarAddressForm, PolarAccountMenu, PolarCheckoutPage },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "physical-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarSavedPaymentMethods,
            Purpose = "Customer's saved cards and wallet payment methods.",
            DataSource = "IStorefrontCustomerService.ListPaymentMethodsAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarPaymentMethodPicker, PolarAccountMenu, PolarCheckoutPage },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarSubscriptionList,
            Purpose = "Customer's active subscriptions.",
            DataSource = "IStorefrontCustomerService.ListSubscriptionsAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarSubscriptionDetail, PolarAccountMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "tenant offers subscriptions" },
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "subscriptions" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },

        // ─── Storefront additions (30) ───────────────────────────────────────
        new()
        {
            Name = PolarCategoryTile,
            Purpose = "Visual tile for one category.",
            DataSource = "IStorefrontCatalogProvider.GetCategoryAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCategoryGrid, PolarDepartmentMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "catalog must have at least one category" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarCategoryGrid,
            Purpose = "Grid of category tiles.",
            DataSource = "IStorefrontCatalogProvider.ListCategoriesAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCategoryTile, PolarDepartmentMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "catalog must have at least one category" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarBreadcrumbTrail,
            Purpose = "Hierarchical breadcrumb navigation (SEO essential).",
            DataSource = "IStorefrontCatalogProvider.GetCategoryHierarchyAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductGrid, PolarProductDetail, PolarCategoryGrid },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "seo", "physical-goods", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("aria-label='breadcrumb' on nav element; current item marked aria-current"),
        },
        new()
        {
            Name = PolarDepartmentMenu,
            Purpose = "Mega-menu of departments + categories.",
            DataSource = "IStorefrontCatalogProvider.GetDepartmentTreeAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCategoryTile, PolarCategoryGrid, PolarBreadcrumbTrail },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "catalog must have a department hierarchy" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "department-store" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("disclosure pattern + arrow-key navigation per WAI-ARIA menubar"),
        },
        new()
        {
            Name = PolarVariantSelector,
            Purpose = "Color/size/material picker; emits polarVariantChanged.",
            DataSource = "IStorefrontCatalogProvider.GetProductVariantsAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductDetail, PolarProductCard, PolarAddToCartButton, PolarStockIndicator },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "products have variants configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "fashion", "electronics" },
            EmitsEvents = new[] { "polarVariantChanged" },
            ListensToEvents = Empty,
            Accessibility = A11y("color swatches paired with text labels; selected state announced"),
        },
        new()
        {
            Name = PolarQuantityStepper,
            Purpose = "+/- quantity input.",
            DataSource = "(client-side only)",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarAddToCartButton, PolarCartDrawer, PolarCartSummary, PolarProductDetail },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = new[] { "polarQuantityChanged" },
            ListensToEvents = Empty,
            Accessibility = A11y("buttons labeled 'increase'/'decrease'; input is a real number spinner"),
        },
        new()
        {
            Name = PolarAddToCartButton,
            Purpose = "Standalone add-to-cart button (vs buy-now).",
            DataSource = "IStorefrontCartService.AddItemAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductCard, PolarProductDetail, PolarQuantityStepper, PolarVariantSelector },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "subscriptions" },
            EmitsEvents = new[] { "polarAddToCartTriggered", "polarCartUpdated" },
            ListensToEvents = new[] { "polarVariantChanged", "polarQuantityChanged" },
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarStockIndicator,
            Purpose = "Inventory badge (in-stock / low / out-of-stock).",
            DataSource = "IStorefrontCatalogProvider.GetInventoryAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductCard, PolarProductDetail, PolarVariantSelector },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarVariantChanged", "polarInventoryChanged" },
            Accessibility = A11y("badge always paired with text label ('In stock', 'Low', 'Out of stock'); color reinforces but does not convey"),
        },
        new()
        {
            Name = PolarCartSummary,
            Purpose = "Full /cart page line-items list with totals.",
            DataSource = "IStorefrontCartService.GetCartAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarQuantityStepper, PolarPromoCodeInput, PolarShippingEstimator, PolarCheckoutButton },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = new[] { "polarCartUpdated", "polarCheckoutTriggered" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarPromoCodeInput,
            Purpose = "Discount code entry.",
            DataSource = "IStorefrontCartService.ApplyPromoCodeAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCartSummary, PolarCartDrawer, PolarCheckoutPage },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "promo codes configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = new[] { "polarPromoCodeApplied" },
            ListensToEvents = Empty,
            Accessibility = A11y("error messages associated with input via aria-describedby"),
        },
        new()
        {
            Name = PolarShippingEstimator,
            Purpose = "ZIP-based shipping rate preview (cart-abandonment reducer).",
            DataSource = "IStorefrontShippingProvider.EstimateAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCartSummary, PolarCheckoutPage },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "shipping provider configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarAddressForm,
            Purpose = "Billing/shipping input + autocomplete.",
            DataSource = "IStorefrontCustomerService.ValidateAddressAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCheckoutPage, PolarSavedAddresses },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("fields use autocomplete tokens (shipping address-line1 etc.) per WCAG 2.2 1.3.5"),
        },
        new()
        {
            Name = PolarPaymentMethodPicker,
            Purpose = "CC / wallet / saved card selection.",
            DataSource = "IStorefrontCheckoutService.ListPaymentMethodsAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCheckoutPage, PolarSavedPaymentMethods, PolarWalletBalance },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "subscriptions" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarPlanPicker,
            Purpose = "2-3 card pricing picker; reference example for the [selected] state attribute pattern.",
            DataSource = "IStorefrontCatalogProvider.ListPlansAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCheckoutButton, PolarCheckoutPage },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "subscription plans configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "subscriptions", "saas", "b2b" },
            EmitsEvents = new[] { "polarPlanSelected" },
            ListensToEvents = Empty,
            Accessibility = A11y("radiogroup semantics; [selected] state announced via aria-checked"),
        },
        new()
        {
            Name = PolarProductMediaGallery,
            Purpose = "Amazon-style image+video gallery with thumbnail strip + lightbox.",
            DataSource = "IStorefrontCatalogProvider.GetProductMediaAsync",
            BackendDependencies = new[] { "PolarSharp.MediaAndFileStorage (task #82) — small additions to PolarMediaFileBase" },
            ComposesWith = new[] { PolarProductDetail, PolarProduct360Viewer, PolarShoppableImage },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "products have media assets uploaded" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "fashion", "electronics" },
            EmitsEvents = new[] { "polarMediaItemSelected" },
            ListensToEvents = new[] { "polarVariantChanged" },
            Accessibility = A11y("alt text required on every image; lightbox keyboard-trap-safe + Esc to close"),
        },
        new()
        {
            Name = Polar2faSetup,
            Purpose = "TOTP + SMS 2FA enrollment UI (QR code, recovery codes, SMS phone number).",
            DataSource = "(ASP.NET Core Identity 2FA APIs)",
            BackendDependencies = new[] { "ASP.NET Core Identity 2FA wiring (task #79)" },
            ComposesWith = new[] { PolarAccountMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "tenant 2FA enabled" },
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "security" },
            EmitsEvents = new[] { "polar2faEnrolled" },
            ListensToEvents = Empty,
            Accessibility = A11y("QR code paired with manual-entry text fallback for screen readers"),
        },
        new()
        {
            Name = Polar2faChallenge,
            Purpose = "2FA 6-digit code challenge during sign-in.",
            DataSource = "(ASP.NET Core Identity 2FA APIs)",
            BackendDependencies = new[] { "ASP.NET Core Identity 2FA wiring (task #79)" },
            ComposesWith = new[] { PolarCheckoutPage, PolarAccountMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "tenant 2FA enabled" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "security" },
            EmitsEvents = new[] { "polar2faChallengePassed", "polar2faChallengeFailed" },
            ListensToEvents = Empty,
            Accessibility = A11y("input is autocomplete='one-time-code' for OS-level paste-from-SMS"),
        },
        new()
        {
            Name = PolarPasskeySetup,
            Purpose = "Passkey/WebAuthn enrollment UI (Touch ID / Face ID / Windows Hello / hardware key).",
            DataSource = "(ASP.NET Core 10 native passkey APIs)",
            BackendDependencies = new[] { "ASP.NET Core 10 passkey wiring (task #80)" },
            ComposesWith = new[] { PolarAccountMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "tenant passkeys enabled" },
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "security" },
            EmitsEvents = new[] { "polarPasskeyEnrolled" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarPasskeyChallenge,
            Purpose = "Passkey WebAuthn assertion challenge during sign-in.",
            DataSource = "(ASP.NET Core 10 native passkey APIs)",
            BackendDependencies = new[] { "ASP.NET Core 10 passkey wiring (task #80)" },
            ComposesWith = new[] { PolarCheckoutPage, PolarAccountMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "tenant passkeys enabled" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "security" },
            EmitsEvents = new[] { "polarPasskeyChallengePassed", "polarPasskeyChallengeFailed" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarSearchResults,
            Purpose = "Full-page search results display.",
            DataSource = "IStorefrontSearchProvider.SearchAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductSearch, PolarProductFilters, PolarProductCard },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "search index populated" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "search", "physical-goods", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarSearchSubmitted", "polarFiltersChanged" },
            Accessibility = A11y("result count announced via aria-live polite region"),
        },
        new()
        {
            Name = PolarRecentlyViewed,
            Purpose = "'Recently viewed' carousel (localStorage + cross-device sync when signed in).",
            DataSource = "IStorefrontCustomerService.GetRecentlyViewedAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductCard },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("carousel arrow controls keyboard-reachable; auto-scroll respects prefers-reduced-motion"),
        },
        new()
        {
            Name = PolarShoppableImage,
            Purpose = "Editorial image with clickable product hotspots (differentiator from Shopify).",
            DataSource = "IStorefrontCatalogProvider.GetShoppableImageAsync",
            BackendDependencies = new[] { "PolarSharp.MediaAndFileStorage (task #82) — shoppable-image hotspot metadata" },
            ComposesWith = new[] { PolarProductMediaGallery, PolarProductCard },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "shoppable images configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "fashion", "lifestyle", "editorial" },
            EmitsEvents = new[] { "polarHotspotActivated" },
            ListensToEvents = Empty,
            Accessibility = A11y("each hotspot is a focusable button with aria-label naming the linked product"),
        },
        new()
        {
            Name = PolarProduct360Viewer,
            Purpose = "Interactive 360-degree product photography; split ship: spin-frames mode v1.4.0, 3D-model mode v1.5+.",
            DataSource = "IStorefrontCatalogProvider.GetProductMediaAsync",
            BackendDependencies = new[] { "PolarSharp.MediaAndFileStorage (task #82) — 360 frame metadata" },
            ComposesWith = new[] { PolarProductDetail, PolarProductMediaGallery },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "products have 360 frame assets uploaded" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            ShipTargetNote = "spin-frames mode v1.4.0; 3D-model mode v1.5+",
            DecisionTreeTags = new[] { "physical-goods", "electronics", "furniture", "fashion" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarVariantChanged" },
            Accessibility = A11y("keyboard arrow keys rotate the view; respects prefers-reduced-motion (still images instead of auto-spin)"),
        },
        new()
        {
            Name = PolarSizeGuide,
            Purpose = "Interactive size guide for apparel (may be superseded by universal sizing engine — task #88).",
            DataSource = "IStorefrontCatalogProvider.GetSizeGuideAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductDetail, PolarVariantSelector },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "size guides configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "fashion", "apparel" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("table semantics with row/column headers; data-cell row+col context announced"),
        },
        new()
        {
            Name = PolarProductQuestions,
            Purpose = "Customer Q&A section.",
            DataSource = "IStorefrontCatalogProvider.ListProductQuestionsAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductDetail },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "Q&A feature enabled" },
            Audience = WcAudience.AnonymousReadsAuthenticatedWrites,
            Deployment = WcDeploymentContext.EmbedAnywhereReadTenantStorefrontWrite,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarProductRating,
            Purpose = "Average star rating + distribution chart.",
            DataSource = "IStorefrontCatalogProvider.GetProductRatingAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductCard, PolarProductDetail, PolarProductReviews },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "reviews feature enabled" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("star icons paired with numeric rating text ('4.3 out of 5')"),
        },
        new()
        {
            Name = PolarProductReviews,
            Purpose = "Full reviews list with filtering + helpful-voting.",
            DataSource = "IStorefrontCatalogProvider.ListProductReviewsAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductDetail, PolarProductRating, PolarProductReviewForm, PolarProductAiSummary },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "reviews feature enabled" },
            Audience = WcAudience.AnonymousReadsAuthenticatedWrites,
            Deployment = WcDeploymentContext.EmbedAnywhereReadTenantStorefrontWrite,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = new[] { "polarReviewHelpfulVoted" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarProductReviewForm,
            Purpose = "Write-a-review (verified purchasers only).",
            DataSource = "IStorefrontCatalogProvider.SubmitReviewAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductReviews, PolarOrderHistoryList },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "reviews feature enabled" },
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront" },
            EmitsEvents = new[] { "polarReviewSubmitted" },
            ListensToEvents = Empty,
            Accessibility = A11y("star-input is a radio group with text-equivalent labels"),
        },
        new()
        {
            Name = PolarProductAiSummary,
            Purpose = "AI-generated 'What customers love / criticize' summary; tenant-AI policy applies.",
            DataSource = "IStorefrontCatalogProvider.GetReviewsAiSummaryAsync",
            BackendDependencies = new[] { "Tenant-AI BYOK + validation (tasks #83 / #84)" },
            ComposesWith = new[] { PolarProductDetail, PolarProductReviews },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "tenant.HasValidatedAiCredentials" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "ai-enabled" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("AI-generated content explicitly labeled so users know it's machine-summarized"),
        },
        new()
        {
            Name = PolarSsoButtonGroup,
            Purpose = "Renders configured SSO providers' sign-in buttons per tenant.",
            DataSource = "ITenantSsoConfigService.GetEnabledProvidersAsync",
            BackendDependencies = new[] { "Per-tenant SSO architecture (task #87)" },
            ComposesWith = new[] { PolarCheckoutPage, PolarAccountMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "at least one SSO provider configured + validated" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "auth", "b2c", "b2b" },
            EmitsEvents = new[] { "polarSsoFlowStarted" },
            ListensToEvents = Empty,
            Accessibility = A11y("each provider button labeled with provider name; icon-only is paired with visually-hidden text"),
        },

        // ─── Reporting additions (13) ────────────────────────────────────────
        new()
        {
            Name = PolarOrderDetail,
            Purpose = "Single-order drilldown view.",
            DataSource = "IStorefrontCustomerService.GetOrderAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarOrderHistoryList, PolarOrderTracking, PolarInvoiceViewer, PolarRefundStatusTracker, PolarReceipt },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarOrderTracking,
            Purpose = "Shipment tracking display.",
            DataSource = "IStorefrontShippingProvider.GetTrackingAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarOrderDetail, PolarOrderHistoryList },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "shipping provider configured" },
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "physical-goods" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarShipmentStatusChanged" },
            Accessibility = A11y("timeline status announced via aria-live region; map fallback to text directions"),
        },
        new()
        {
            Name = PolarInvoiceViewer,
            Purpose = "Invoice PDF download + print view.",
            DataSource = "IStorefrontCustomerService.GetInvoiceAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarOrderDetail, PolarOrderHistoryList },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("PDF includes tagged-PDF structure; HTML print fallback for screen reader users"),
        },
        new()
        {
            Name = PolarReceipt,
            Purpose = "Post-purchase confirmation receipt.",
            DataSource = "IStorefrontCheckoutService.GetReceiptAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarOrderDetail },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "subscriptions" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarOrderPlaced" },
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarSpendSummary,
            Purpose = "'You've spent $X this year' personalized stats.",
            DataSource = "IStorefrontCustomerService.GetSpendSummaryAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarAccountMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarSubscriptionDetail,
            Purpose = "Single-subscription drilldown (change plan / cancel / pause).",
            DataSource = "IStorefrontCustomerService.GetSubscriptionAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarSubscriptionList, PolarPlanPicker },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "tenant offers subscriptions" },
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "subscriptions" },
            EmitsEvents = new[] { "polarSubscriptionChanged" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarRefundStatusTracker,
            Purpose = "Refund-request timeline.",
            DataSource = "IStorefrontCustomerService.GetRefundStatusAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarOrderDetail },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarRefundStatusChanged" },
            Accessibility = A11y("status timeline labeled with stage names (Submitted/Approved/Refunded)"),
        },
        new()
        {
            Name = PolarBenefitList,
            Purpose = "Active customer benefits (license keys, downloads, Discord roles).",
            DataSource = "IStorefrontCustomerService.ListBenefitsAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarLicenseKeyDisplay, PolarDownloadList, PolarAccountMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "digital-goods", "subscriptions" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarLicenseKeyDisplay,
            Purpose = "Single license-key with copy button + activation status.",
            DataSource = "IStorefrontCustomerService.GetLicenseKeyAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarBenefitList },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "digital-goods" },
            EmitsEvents = new[] { "polarLicenseKeyCopied" },
            ListensToEvents = Empty,
            Accessibility = A11y("copy button announces 'copied' via aria-live after copy succeeds"),
        },
        new()
        {
            Name = PolarDownloadList,
            Purpose = "Customer's downloadable files.",
            DataSource = "IStorefrontCustomerService.ListDownloadsAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarBenefitList },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarLoyaltyStatus,
            Purpose = "Loyalty tier + points + progress toward next tier.",
            DataSource = "IStorefrontLoyaltyService.GetStatusAsync",
            BackendDependencies = new[] { "Loyalty system backend (task #85)" },
            ComposesWith = new[] { PolarAccountMenu, PolarSpendSummary },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "loyalty program enabled for tenant" },
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "loyalty", "b2c" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarOrderPlaced" },
            Accessibility = A11y("progress bar uses aria-valuenow / aria-valuemax; tier names announced"),
        },
        new()
        {
            Name = PolarReferralTracker,
            Purpose = "Referrals + rewards earned.",
            DataSource = "IStorefrontReferralService.GetSummaryAsync",
            BackendDependencies = new[] { "Referral system backend (task #86)" },
            ComposesWith = new[] { PolarAccountMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "referral program enabled for tenant" },
            Audience = WcAudience.AuthenticatedRequired,
            Deployment = WcDeploymentContext.TenantStorefrontOnly,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "tenant-storefront", "referrals", "b2c" },
            EmitsEvents = new[] { "polarReferralLinkCopied" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarImpactStatement,
            Purpose = "Sustainability messaging ('planted 3 trees' / 'offset 50kg CO2').",
            DataSource = "IStorefrontImpactService.GetCustomerImpactAsync",
            BackendDependencies = new[] { "Impact / sustainability tracking backend (v1.5+)" },
            ComposesWith = new[] { PolarReceipt, PolarAccountMenu },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "impact program configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V15Plus,
            DecisionTreeTags = new[] { "sustainability", "b2c" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("impact metrics paired with units and context text"),
        },

        // ─── Merchandising additions (24) ────────────────────────────────────
        new()
        {
            Name = PolarPromoBanner,
            Purpose = "Site-wide promotional banner.",
            DataSource = "IStorefrontMerchandisingService.GetActivePromotionsAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCountdownTimer },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "active promotion configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "b2c" },
            EmitsEvents = new[] { "polarPromoBannerDismissed" },
            ListensToEvents = Empty,
            Accessibility = A11y("dismissible region uses role=region + aria-label; close button keyboard-reachable"),
        },
        new()
        {
            Name = PolarCountdownTimer,
            Purpose = "Sale-ending countdown ticker.",
            DataSource = "(client-side; promotion end-time supplied as attribute)",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarPromoBanner, PolarFlashSaleCard, PolarPreOrderCard },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "b2c" },
            EmitsEvents = new[] { "polarCountdownExpired" },
            ListensToEvents = Empty,
            Accessibility = A11y("time announced via aria-live polite; expiry triggers a final announcement"),
        },
        new()
        {
            Name = PolarFlashSaleCard,
            Purpose = "Time-limited product card with countdown.",
            DataSource = "IStorefrontCatalogProvider.GetProductAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCountdownTimer, PolarAddToCartButton, PolarStockIndicator },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "flash sale configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "b2c" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarCountdownExpired" },
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarTrustBadges,
            Purpose = "Payment + security + guarantee badges.",
            DataSource = "(tenant config; payment methods + guarantee policies)",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCheckoutPage, PolarCartSummary, PolarProductDetail },
            ConflictsWith = Empty,
            RequiredTenantConfig = Empty,
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("each badge icon paired with text label (e.g. 'Visa accepted')"),
        },
        new()
        {
            Name = PolarFeaturedCollection,
            Purpose = "'Featured items for {category}' carousel.",
            DataSource = "IStorefrontMerchandisingService.GetFeaturedCollectionAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductCard },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "featured collection configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "b2c" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("carousel arrow controls keyboard-reachable; auto-scroll respects prefers-reduced-motion"),
        },
        new()
        {
            Name = PolarCuratedCollection,
            Purpose = "Manually-curated product list.",
            DataSource = "IStorefrontMerchandisingService.GetCuratedCollectionAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductCard, PolarProductGrid },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "curated collection configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "editorial" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarOccasionShop,
            Purpose = "Themed seasonal collections.",
            DataSource = "IStorefrontMerchandisingService.GetOccasionCollectionAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductCard, PolarCategoryGrid },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "seasonal collection configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "seasonal", "b2c" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarUpSellCard,
            Purpose = "'Upgrade to Premium for $X more' inline upsell.",
            DataSource = "IStorefrontMerchandisingService.GetUpSellAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCartDrawer, PolarCartSummary, PolarCheckoutPage },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "upsell rules configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "subscriptions" },
            EmitsEvents = new[] { "polarUpSellAccepted" },
            ListensToEvents = new[] { "polarCartUpdated" },
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarBuyXGetY,
            Purpose = "'Buy 2 get 1 free' promo display.",
            DataSource = "IStorefrontMerchandisingService.GetBuyXGetYAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductCard, PolarCartSummary },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "BXGY promotion configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "b2c" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarBundleOffer,
            Purpose = "'Save 20% on this bundle' display.",
            DataSource = "IStorefrontMerchandisingService.GetBundleOfferAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductCard, PolarAddToCartButton },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "bundle configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = new[] { "polarBundleAddedToCart" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarDiscountTierBadge,
            Purpose = "Cart-aware tier progress ('$50 more for 10% off').",
            DataSource = "IStorefrontCartService.GetDiscountTiersAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCartDrawer, PolarCartSummary },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "tiered discount configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarCartUpdated" },
            Accessibility = A11y("progress to next tier announced via aria-live; tier-reached state announces benefit"),
        },
        new()
        {
            Name = PolarEmailCapture,
            Purpose = "Newsletter signup with discount incentive.",
            DataSource = "IStorefrontMarketingService.SubscribeAsync",
            BackendDependencies = Empty,
            ComposesWith = Empty,
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "newsletter list configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "marketing", "b2c" },
            EmitsEvents = new[] { "polarNewsletterSubscribed" },
            ListensToEvents = Empty,
            Accessibility = A11y("email input is autocomplete='email' + validated with descriptive errors"),
        },
        new()
        {
            Name = PolarRecentlyPurchasedByOthers,
            Purpose = "Social-proof ticker showing recent purchases.",
            DataSource = "IStorefrontMerchandisingService.GetRecentPurchasesTickerAsync",
            BackendDependencies = Empty,
            ComposesWith = Empty,
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "tenant has recent purchase activity" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V140,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "social-proof" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("rotation pauses on hover/focus; respects prefers-reduced-motion"),
        },
        new()
        {
            Name = PolarCrossSellGrid,
            Purpose = "'Customers also bought' grid.",
            DataSource = "IStorefrontMerchandisingService.GetCrossSellAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarProductCard, PolarProductDetail, PolarCartDrawer },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "cross-sell relationships configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V14xUnscheduled,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarCouponPopup,
            Purpose = "Triggered modal with discount code.",
            DataSource = "IStorefrontMerchandisingService.GetCouponPopupAsync",
            BackendDependencies = Empty,
            ComposesWith = Empty,
            ConflictsWith = new[] { PolarExitIntentModal },
            RequiredTenantConfig = new[] { "coupon configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V14xUnscheduled,
            DecisionTreeTags = new[] { "marketing", "b2c" },
            EmitsEvents = new[] { "polarCouponCopied" },
            ListensToEvents = Empty,
            Accessibility = A11y("dialog uses role=dialog + aria-modal=true; focus trap; Esc closes; restores focus to trigger"),
        },
        new()
        {
            Name = PolarExitIntentModal,
            Purpose = "Modal triggered when the cursor leaves the viewport.",
            DataSource = "IStorefrontMerchandisingService.GetExitIntentOfferAsync",
            BackendDependencies = Empty,
            ComposesWith = Empty,
            ConflictsWith = new[] { PolarCouponPopup },
            RequiredTenantConfig = new[] { "exit-intent offer configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V14xUnscheduled,
            DecisionTreeTags = new[] { "marketing", "b2c" },
            EmitsEvents = new[] { "polarExitIntentTriggered" },
            ListensToEvents = Empty,
            Accessibility = A11y("dialog uses role=dialog + aria-modal=true; touch devices fall back to scroll-up trigger"),
        },
        new()
        {
            Name = PolarPreOrderCard,
            Purpose = "Pre-order product card with launch countdown.",
            DataSource = "IStorefrontCatalogProvider.GetPreOrderProductAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCountdownTimer, PolarAddToCartButton },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "pre-order product configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V14xUnscheduled,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods" },
            EmitsEvents = Empty,
            ListensToEvents = new[] { "polarCountdownExpired" },
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarWaitlistButton,
            Purpose = "'Notify me when back in stock' signup.",
            DataSource = "IStorefrontMerchandisingService.JoinWaitlistAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarStockIndicator, PolarProductDetail },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "waitlist feature enabled" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V14xUnscheduled,
            DecisionTreeTags = new[] { "physical-goods" },
            EmitsEvents = new[] { "polarWaitlistJoined" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarCharityDonationAddOn,
            Purpose = "Cart-level charity donation.",
            DataSource = "IStorefrontCartService.AddCharityDonationAsync",
            BackendDependencies = Empty,
            ComposesWith = new[] { PolarCartSummary, PolarCartDrawer, PolarCheckoutPage },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "charity partner configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V14xUnscheduled,
            DecisionTreeTags = new[] { "sustainability", "b2c" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarBuildYourOwnBundle,
            Purpose = "Interactive bundle builder.",
            DataSource = "IStorefrontMerchandisingService.GetBundleBuilderAsync",
            BackendDependencies = new[] { "Bundle-builder backend (v1.5+)" },
            ComposesWith = new[] { PolarProductCard, PolarAddToCartButton },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "bundle-builder template configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V15Plus,
            DecisionTreeTags = new[] { "physical-goods", "b2c" },
            EmitsEvents = new[] { "polarBundleCompleted" },
            ListensToEvents = Empty,
            Accessibility = A11y("multi-step builder uses aria-current step + clear validation messaging"),
        },
        new()
        {
            Name = PolarGiftFinderWizard,
            Purpose = "AI-driven conversational gift recommender; tenant-AI policy applies.",
            DataSource = "IStorefrontMerchandisingService.GetGiftRecommendationsAsync",
            BackendDependencies = new[] { "Tenant-AI BYOK + validation (tasks #83 / #84)" },
            ComposesWith = new[] { PolarProductCard },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "tenant.HasValidatedAiCredentials" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V15Plus,
            DecisionTreeTags = new[] { "physical-goods", "ai-enabled", "b2c" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("conversational UI provides text alternatives to any visual recommendations"),
        },
        new()
        {
            Name = PolarTipJar,
            Purpose = "Tip the maker/creator.",
            DataSource = "IStorefrontTipService.SubmitTipAsync",
            BackendDependencies = new[] { "Tip-jar backend (v1.5+)" },
            ComposesWith = new[] { PolarProductDetail, PolarReceipt },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "creator profile enabled" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V15Plus,
            DecisionTreeTags = new[] { "creator", "digital-goods", "b2c" },
            EmitsEvents = new[] { "polarTipSubmitted" },
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarInfluencerStorefront,
            Purpose = "Curated micro-storefront by affiliate.",
            DataSource = "IStorefrontAffiliateService.GetInfluencerStorefrontAsync",
            BackendDependencies = new[] { "Affiliate / influencer backend (v1.5+)" },
            ComposesWith = new[] { PolarProductCard, PolarProductGrid },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "affiliate program enabled" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhereReadTenantStorefrontWrite,
            ShipTarget = WcShipTarget.V15Plus,
            DecisionTreeTags = new[] { "physical-goods", "digital-goods", "creator", "b2c" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = DefaultA11y,
        },
        new()
        {
            Name = PolarLiveShoppingEvent,
            Purpose = "Streaming live-shopping event with chat + inline product cards.",
            DataSource = "IStorefrontLiveService.GetEventAsync",
            BackendDependencies = new[] { "Live-shopping backend (v1.5+)" },
            ComposesWith = new[] { PolarProductCard, PolarAddToCartButton },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "live-shopping enabled + stream configured" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.V15Plus,
            DecisionTreeTags = new[] { "physical-goods", "b2c", "live" },
            EmitsEvents = new[] { "polarLiveProductFeatured" },
            ListensToEvents = Empty,
            Accessibility = A11y("captions required on video stream; chat reachable via screen reader"),
        },

        // ─── Deferred to dedicated planning (2) ──────────────────────────────
        new()
        {
            Name = PolarProductComparisonTable,
            Purpose = "Side-by-side product comparison (specs schema + selection persistence + comparison overlay UX + accessibility). Deferred to task #78.",
            DataSource = "IStorefrontCatalogProvider.CompareProductsAsync",
            BackendDependencies = new[] { "Side-by-side comparison capability (task #78)" },
            ComposesWith = new[] { PolarProductCard, PolarProductDetail, PolarPlanComparisonTable },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "comparison schema defined" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.DeferredToPlanning,
            ShipTargetNote = "deferred to task #78 dedicated planning session",
            DecisionTreeTags = new[] { "physical-goods", "electronics", "comparison" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("comparison table uses proper row/column headers; cell context fully announced"),
        },
        new()
        {
            Name = PolarPlanComparisonTable,
            Purpose = "Subscription plan feature-matrix comparison. Deferred to task #78.",
            DataSource = "IStorefrontCatalogProvider.ComparePlansAsync",
            BackendDependencies = new[] { "Side-by-side comparison capability (task #78)" },
            ComposesWith = new[] { PolarPlanPicker, PolarProductComparisonTable },
            ConflictsWith = Empty,
            RequiredTenantConfig = new[] { "subscription plans configured + feature matrix defined" },
            Audience = WcAudience.AnonymousOk,
            Deployment = WcDeploymentContext.EmbedAnywhere,
            ShipTarget = WcShipTarget.DeferredToPlanning,
            ShipTargetNote = "deferred to task #78 dedicated planning session",
            DecisionTreeTags = new[] { "subscriptions", "saas", "b2b", "comparison" },
            EmitsEvents = Empty,
            ListensToEvents = Empty,
            Accessibility = A11y("comparison table uses proper row/column headers; checkmark/x icons paired with text"),
        },
    };

    /// <summary>The full WC registry keyed by tag name.</summary>
    public static IReadOnlyDictionary<string, WcDescriptor> AllWcs { get; } =
        new ReadOnlyDictionary<string, WcDescriptor>(
            DescriptorsArray.ToDictionary(d => d.Name));

    /// <summary>
    /// Returns the WCs assigned to the supplied <paramref name="target"/> release window.
    /// </summary>
    /// <param name="target">The release window to filter by.</param>
    /// <returns>A read-only list of descriptors matching the ship target.</returns>
    /// <example>
    /// <code>
    /// var launchSet = WcCatalog.ByShipTarget(WcShipTarget.V140);
    /// // → all WCs shipping in v1.4.0
    /// </code>
    /// </example>
    public static IReadOnlyList<WcDescriptor> ByShipTarget(WcShipTarget target) =>
        DescriptorsArray.Where(d => d.ShipTarget == target).ToArray();

    /// <summary>
    /// Returns the WCs that may be deployed in the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The deployment context to filter by.</param>
    /// <returns>A read-only list of descriptors matching the deployment context.</returns>
    public static IReadOnlyList<WcDescriptor> ByDeployment(WcDeploymentContext context) =>
        DescriptorsArray.Where(d => d.Deployment == context).ToArray();

    /// <summary>
    /// Returns the WCs whose <see cref="WcDescriptor.DecisionTreeTags"/> include the supplied
    /// <paramref name="tag"/>. Tags are matched case-sensitively.
    /// </summary>
    /// <param name="tag">The decision-tree tag to filter by (e.g. <c>physical-goods</c>, <c>subscriptions</c>).</param>
    /// <returns>A read-only list of descriptors carrying the tag.</returns>
    /// <example>
    /// <code>
    /// var subscriptionWcs = WcCatalog.ByDecisionTreeTag("subscriptions");
    /// </code>
    /// </example>
    public static IReadOnlyList<WcDescriptor> ByDecisionTreeTag(string tag) =>
        DescriptorsArray.Where(d => d.DecisionTreeTags.Contains(tag)).ToArray();
}
