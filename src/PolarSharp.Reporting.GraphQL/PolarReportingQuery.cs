using HotChocolate;
using HotChocolate.Authorization;
using PolarSharp;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.MultiTenant.Identity.Authorization;
using PolarSharp.Reporting;
using PolarSharp.Reporting.Drilldown;
using PolarSharp.Reporting.Reports;

namespace PolarSharp.Reporting.GraphQL;

/// <summary>
/// Hot Chocolate GraphQL Query type that mirrors <see cref="IPolarReportingClient"/> —
/// every interface method is exposed as a single Query field. Resolvers unwrap the
/// <c>Result&lt;T, PolarError&gt;</c> envelope; failures surface as a Hot Chocolate
/// <see cref="GraphQLException"/>, the standard error path callers can decode from the
/// <c>errors</c> array on every response.
/// </summary>
/// <remarks>
/// <para>
/// All fields require the <see cref="PolarPermission.ViewReports"/> permission EXCEPT
/// <see cref="ErrorAuditAsync"/>, which additionally requires
/// <see cref="PolarPermission.ViewAuditLog"/>. Authorization is delegated to the existing
/// <c>PolarPermissionAuthorizationHandler</c> via the
/// <c>PolarSharp.Permission.{PolarPermission}</c> policy names produced by
/// <see cref="PolarAuthorizationPolicies.RegisterAllBuiltIn"/> — no parallel security
/// logic for GraphQL.
/// </para>
/// <para>
/// Tenant scope flows automatically: GraphQL resolvers run in the same DI scope as the
/// HTTP request, Finbuckle middleware populates the tenant context before the GraphQL
/// pipeline runs, and the injected <see cref="IPolarReportingClient"/> reads from a
/// <c>PolarReportingDbContext</c> that respects the global tenant filter.
/// </para>
/// </remarks>
public sealed class PolarReportingQuery
{
    /// <summary>Aggregate transaction roll-up over a date range.</summary>
    /// <param name="input">The request shape mirroring <see cref="TransactionReportRequest"/>.</param>
    /// <param name="client">Injected reporting client (Hot Chocolate <c>[Service]</c>).</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>The transaction report on success.</returns>
    /// <exception cref="GraphQLException">Thrown when the underlying reporting call fails.</exception>
    [Authorize(Policy = PolarAuthorizationPolicies.PermissionPrefix + nameof(PolarPermission.ViewReports))]
    public Task<TransactionReport> TransactionsAsync(
        TransactionReportRequestInput input,
        [Service] IPolarReportingClient client,
        CancellationToken ct)
        => UnwrapAsync(c => client.GetTransactionsAsync(input.ToRequest(), c), ct);

    /// <summary>Subscription metrics — MRR / ARR / churn / cohort retention.</summary>
    /// <param name="input">The request shape mirroring <see cref="SubscriptionReportRequest"/>.</param>
    /// <param name="client">Injected reporting client.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>The subscription report on success.</returns>
    /// <exception cref="GraphQLException">Thrown when the underlying reporting call fails.</exception>
    [Authorize(Policy = PolarAuthorizationPolicies.PermissionPrefix + nameof(PolarPermission.ViewReports))]
    public Task<SubscriptionReport> SubscriptionsAsync(
        SubscriptionReportRequestInput input,
        [Service] IPolarReportingClient client,
        CancellationToken ct)
        => UnwrapAsync(c => client.GetSubscriptionsAsync(input.ToRequest(), c), ct);

    /// <summary>Order counts + fulfilment latency.</summary>
    /// <param name="input">The request shape mirroring <see cref="OrderReportRequest"/>.</param>
    /// <param name="client">Injected reporting client.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>The order report on success.</returns>
    /// <exception cref="GraphQLException">Thrown when the underlying reporting call fails.</exception>
    [Authorize(Policy = PolarAuthorizationPolicies.PermissionPrefix + nameof(PolarPermission.ViewReports))]
    public Task<OrderReport> OrdersAsync(
        OrderReportRequestInput input,
        [Service] IPolarReportingClient client,
        CancellationToken ct)
        => UnwrapAsync(c => client.GetOrdersAsync(input.ToRequest(), c), ct);

    /// <summary>Customer roll-up + lifecycle segments.</summary>
    /// <param name="input">The request shape mirroring <see cref="CustomerReportRequest"/>.</param>
    /// <param name="client">Injected reporting client.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>The customer report on success.</returns>
    /// <exception cref="GraphQLException">Thrown when the underlying reporting call fails.</exception>
    [Authorize(Policy = PolarAuthorizationPolicies.PermissionPrefix + nameof(PolarPermission.ViewReports))]
    public Task<CustomerReport> CustomersAsync(
        CustomerReportRequestInput input,
        [Service] IPolarReportingClient client,
        CancellationToken ct)
        => UnwrapAsync(c => client.GetCustomersAsync(input.ToRequest(), c), ct);

    /// <summary>Per-customer entitlement detail.</summary>
    /// <param name="customerId">The Polar customer id to look up.</param>
    /// <param name="client">Injected reporting client.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>The customer entitlements report on success.</returns>
    /// <exception cref="GraphQLException">Thrown when the underlying reporting call fails.</exception>
    [Authorize(Policy = PolarAuthorizationPolicies.PermissionPrefix + nameof(PolarPermission.ViewReports))]
    public Task<CustomerEntitlementsReport> CustomerEntitlementsAsync(
        string customerId,
        [Service] IPolarReportingClient client,
        CancellationToken ct)
        => UnwrapAsync(c => client.GetCustomerEntitlementsAsync(customerId, c), ct);

    /// <summary>Operational error / audit roll-up. Requires <see cref="PolarPermission.ViewAuditLog"/>.</summary>
    /// <param name="input">The request shape mirroring <see cref="ErrorAuditRequest"/>.</param>
    /// <param name="client">Injected reporting client.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>The error audit report on success.</returns>
    /// <exception cref="GraphQLException">Thrown when the underlying reporting call fails.</exception>
    [Authorize(Policy = PolarAuthorizationPolicies.PermissionPrefix + nameof(PolarPermission.ViewAuditLog))]
    public Task<ErrorAuditReport> ErrorAuditAsync(
        ErrorAuditRequestInput input,
        [Service] IPolarReportingClient client,
        CancellationToken ct)
        => UnwrapAsync(c => client.GetErrorAuditAsync(input.ToRequest(), c), ct);

    /// <summary>Top-level drilldown — paged list of every customer in the tenant.</summary>
    /// <param name="input">The request shape mirroring <see cref="CustomerListRequest"/>.</param>
    /// <param name="client">Injected reporting client.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>A paged result of customer rows on success.</returns>
    /// <exception cref="GraphQLException">Thrown when the underlying reporting call fails.</exception>
    [Authorize(Policy = PolarAuthorizationPolicies.PermissionPrefix + nameof(PolarPermission.ViewReports))]
    public Task<PagedResult<CustomerListRow>> ListCustomersAsync(
        CustomerListRequestInput input,
        [Service] IPolarReportingClient client,
        CancellationToken ct)
        => UnwrapAsync(c => client.ListCustomersAsync(input.ToRequest(), c), ct);

    /// <summary>Mid-level drilldown — paged list of orders for one customer.</summary>
    /// <param name="customerId">The Polar customer id whose orders to list.</param>
    /// <param name="input">The request shape mirroring <see cref="OrderListRequest"/>.</param>
    /// <param name="client">Injected reporting client.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>A paged result of order summary rows on success.</returns>
    /// <exception cref="GraphQLException">Thrown when the underlying reporting call fails.</exception>
    [Authorize(Policy = PolarAuthorizationPolicies.PermissionPrefix + nameof(PolarPermission.ViewReports))]
    public Task<PagedResult<OrderSummaryRow>> ListOrdersForCustomerAsync(
        string customerId,
        OrderListRequestInput input,
        [Service] IPolarReportingClient client,
        CancellationToken ct)
        => UnwrapAsync(c => client.ListOrdersForCustomerAsync(customerId, input.ToRequest(), c), ct);

    /// <summary>Bottom-level drilldown — full detail for one order (line items + refunds + benefit grants).</summary>
    /// <param name="orderId">The Polar order id to retrieve.</param>
    /// <param name="client">Injected reporting client.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>The order drilldown detail on success.</returns>
    /// <exception cref="GraphQLException">Thrown when the underlying reporting call fails.</exception>
    [Authorize(Policy = PolarAuthorizationPolicies.PermissionPrefix + nameof(PolarPermission.ViewReports))]
    public Task<OrderDrilldownDetail> GetOrderDrilldownAsync(
        string orderId,
        [Service] IPolarReportingClient client,
        CancellationToken ct)
        => UnwrapAsync(c => client.GetOrderDrilldownAsync(orderId, c), ct);

    private static async Task<T> UnwrapAsync<T>(
        Func<CancellationToken, Task<Result<T, PolarError>>> call,
        CancellationToken ct)
    {
        var result = await call(ct).ConfigureAwait(false);
        return result.Match<T>(
            onSuccess: value => value,
            onFailure: error => throw new GraphQLException(ErrorBuilder.New()
                .SetMessage(error.Message)
                .SetCode(error.GetType().Name)
                .SetExtension("requestId", error.RequestId)
                .Build()));
    }
}
