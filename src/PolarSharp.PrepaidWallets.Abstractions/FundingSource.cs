namespace PolarSharp.PrepaidWallets.Abstractions;

/// <summary>
/// Identifies where a wallet funding event came from — Polar.sh checkout, Stripe charge, PayPal
/// order, an admin manual credit, etc. The wallet-core only needs the discriminator plus a free-form
/// reference for downstream reconciliation; the actual payment instrument never enters the wallet
/// boundary.
/// </summary>
/// <param name="Kind">The funding-source kind (free-form string for forward-compatibility with future processors).</param>
/// <param name="ExternalReference">An optional processor-side reference (e.g. a Stripe charge id).</param>
/// <remarks>
/// <para>
/// Per Case Study 02, the SaaS itself "must not need to take custody of customer payment
/// instruments to take its cut of the economics." The wallet ledger therefore records only the
/// kind + reference; the payment-processor-specific details belong to the bridge that consumed
/// the webhook.
/// </para>
/// </remarks>
public sealed record FundingSource(string Kind, Option<string> ExternalReference)
{
    /// <summary>Construct a Polar.sh funding source.</summary>
    /// <param name="polarOrderId">The Polar order id from the <c>order.paid</c> webhook.</param>
    /// <returns>A funding source tagged as <c>polar</c>.</returns>
    public static FundingSource Polar(string polarOrderId) =>
        new("polar", Option<string>.Some(polarOrderId));

    /// <summary>Construct a Stripe funding source.</summary>
    /// <param name="stripeChargeId">The Stripe charge id.</param>
    /// <returns>A funding source tagged as <c>stripe</c>.</returns>
    public static FundingSource Stripe(string stripeChargeId) =>
        new("stripe", Option<string>.Some(stripeChargeId));

    /// <summary>Construct a PayPal funding source.</summary>
    /// <param name="paypalOrderId">The PayPal order id.</param>
    /// <returns>A funding source tagged as <c>paypal</c>.</returns>
    public static FundingSource PayPal(string paypalOrderId) =>
        new("paypal", Option<string>.Some(paypalOrderId));

    /// <summary>Construct a manual admin credit (e.g. tenant operator topping up a customer wallet).</summary>
    /// <param name="note">Free-form operator note (e.g. ticket reference).</param>
    /// <returns>A funding source tagged as <c>manual</c>.</returns>
    public static FundingSource Manual(string note) =>
        new("manual", Option<string>.Some(note));
}
