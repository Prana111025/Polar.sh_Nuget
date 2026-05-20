namespace PolarSharp.EcommerceStorefronts.Abstractions.Catalog;

/// <summary>
/// Authentication-state requirement for a PolarSharp Web Component. Used by the agent-driven
/// marketplace constructor to filter the catalog when choosing WCs for a niche profile that
/// may or may not have authenticated session state available.
/// </summary>
public enum WcAudience
{
    /// <summary>WC works without an authenticated session (e.g. product browse, public cart).</summary>
    AnonymousOk = 0,

    /// <summary>WC requires the customer to be signed in (e.g. order history, saved addresses).</summary>
    AuthenticatedRequired = 1,

    /// <summary>
    /// WC allows anonymous reads but writes require an authenticated session
    /// (e.g. product reviews/questions: anyone may read, only verified purchasers may post).
    /// </summary>
    AnonymousReadsAuthenticatedWrites = 2,
}
