using System.Text.Json.Serialization;

namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>
/// Tax-bucket category of tokens credited to a wallet. Distinct from <see cref="FundingSource"/>,
/// which captures the payment-processor that delivered the funds; <see cref="FundingSourceKind"/>
/// captures the *legal / tax-treatment* category of the tokens themselves.
/// </summary>
/// <remarks>
/// <para>
/// The Phase 22.5 "Wallet Tax Responsibility" framework consumes this enum to compute tax on
/// wallet-spend events: customer-cash-funded tokens are typically fully taxable on the new sale,
/// whereas tenant-issued reward tokens are often treated as discounts that reduce taxable basis.
/// The enum value must be present on <see cref="Events.WalletFunded"/> and
/// <see cref="Events.WalletCredited"/> events at recording time — recomputing the category from
/// event history is not viable at scale.
/// </para>
/// <para>
/// Per Case Study 02 and the v1.3 WTR framework, tax computation lives downstream of the wallet
/// in a separate projection. The wallet's only job here is to record which bucket each token
/// belongs to so the projection has the data it needs.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<FundingSourceKind>))]
public enum FundingSourceKind
{
    /// <summary>
    /// Tokens funded by the customer paying through Polar, Stripe, or PayPal at funding time.
    /// Tax treatment: typically fully taxable on the new sale.
    /// </summary>
    CustomerCashFunded = 0,

    /// <summary>
    /// Tokens loaded into the wallet via gift-card activation. Tax treatment: typically fully
    /// taxable on the new sale (redemption is the taxable event, per most US states' gift-card
    /// rules).
    /// </summary>
    GiftCardActivation = 1,

    /// <summary>
    /// Tokens credited back to the wallet from a prior refund (refund-as-credit). Tax treatment:
    /// typically fully taxable on the new sale (it's the customer's own money returned).
    /// </summary>
    RefundAsCredit = 2,

    /// <summary>
    /// Tokens issued as tenant rewards, loyalty credits, or promotional grants. Tax treatment:
    /// typically reduces taxable basis (discount); jurisdiction-dependent.
    /// </summary>
    TenantPromotionalGrant = 3,

    /// <summary>
    /// Tokens issued by the tenant operator as compensation for a service issue or bug.
    /// Tax treatment: typically reduces taxable basis (discount); jurisdiction-dependent.
    /// </summary>
    TenantBugFixCompensation = 4,

    /// <summary>
    /// Tokens issued as a trial / signup bonus. Tax treatment: typically reduces taxable basis
    /// (discount).
    /// </summary>
    TrialCredit = 5,
}
