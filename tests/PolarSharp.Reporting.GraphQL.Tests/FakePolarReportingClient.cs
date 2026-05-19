using PolarSharp;
using PolarSharp.Reporting;
using PolarSharp.Reporting.Drilldown;
using PolarSharp.Reporting.Reports;

namespace PolarSharp.Reporting.GraphQL.Tests;

/// <summary>
/// Hand-rolled <see cref="IPolarReportingClient"/> stub returning canned values. Test code
/// flips the <c>FailNext</c> fields to inject a <see cref="PolarError"/> and exercise the
/// GraphQL error path; the default state returns a known-good report shape per method so
/// the end-to-end query tests assert against deterministic JSON.
/// </summary>
internal sealed class FakePolarReportingClient : IPolarReportingClient
{
    public PolarError? FailNext { get; set; }

    public TransactionReport TransactionResponse { get; set; } = new()
    {
        PeriodStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        PeriodEnd = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
        Currency = "USD",
        GrossRevenue = 1_000_00,
        RefundedAmount = 50_00,
        NetRevenue = 950_00,
        OrderCount = 12,
        RefundCount = 1,
        AverageOrderValue = 83_33,
    };

    public OrderDrilldownDetail OrderDrilldownResponse { get; set; } = new()
    {
        OrderId = "ord_test",
        OrderNumber = "INV-0001",
        CustomerId = "cust_42",
        CustomerEmail = "buyer@example.com",
        Status = "paid",
        Amount = 9999,
        TaxAmount = 800,
        Currency = "USD",
        CreatedAt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
        FulfilledAt = new DateTimeOffset(2026, 3, 1, 12, 5, 0, TimeSpan.Zero),
        LineItems =
        [
            new OrderLineItemRow("prod_a", "Pro Plan", "price_a", 1, 9199, 9199, 0, 800),
        ],
        Refunds = [],
        BenefitGrants =
        [
            new BenefitGrantRow("ben_a", "License key", "license_keys", true, new DateTimeOffset(2026, 3, 1, 12, 5, 0, TimeSpan.Zero), null),
        ],
    };

    public PagedResult<CustomerListRow> CustomerListResponse { get; set; } = new()
    {
        Rows =
        [
            new CustomerListRow
            {
                CustomerId = "cust_1",
                Email = "first@example.com",
                Name = "First Customer",
                OrderCount = 3,
                LifetimeValue = 25000,
                Currency = "USD",
                CreatedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new CustomerListRow
            {
                CustomerId = "cust_2",
                Email = "second@example.com",
                Name = null,
                OrderCount = 1,
                LifetimeValue = 2500,
                Currency = "USD",
                CreatedAt = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
            },
        ],
        TotalCount = 2,
        Page = 0,
        PageSize = 50,
    };

    public ErrorAuditReport ErrorAuditResponse { get; set; } = new()
    {
        WebhookDeliveryFailures = 1,
        SignatureVerificationFailures = 0,
        CircuitBreakerOpenEvents = 0,
        RateLimitHits = 2,
        ApiErrorsByStatus = 3,
    };

    private Result<T, PolarError> ToResult<T>(T value) =>
        FailNext is null
            ? Result<T, PolarError>.Success(value)
            : Result<T, PolarError>.Failure(FailNext);

    public Task<Result<TransactionReport, PolarError>> GetTransactionsAsync(TransactionReportRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult(TransactionResponse));

    public Task<Result<SubscriptionReport, PolarError>> GetSubscriptionsAsync(SubscriptionReportRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult(new SubscriptionReport
        {
            Mrr = 100_000,
            Arr = 1_200_000,
            ActiveSubscriptions = 25,
            NewSubscriptions = 5,
            CanceledSubscriptions = 1,
            ChurnRate = 0.04m,
            ExpansionRevenue = 0,
        }));

    public Task<Result<OrderReport, PolarError>> GetOrdersAsync(OrderReportRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult(new OrderReport
        {
            Total = 100,
            Fulfilled = 90,
            Pending = 8,
            Failed = 2,
            MedianFulfillmentLatency = TimeSpan.FromMinutes(15),
        }));

    public Task<Result<ErrorAuditReport, PolarError>> GetErrorAuditAsync(ErrorAuditRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult(ErrorAuditResponse));

    public Task<Result<CustomerReport, PolarError>> GetCustomersAsync(CustomerReportRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult(new CustomerReport
        {
            TotalCustomers = 200,
            NewCustomers = 15,
            AverageLifetimeValue = 50000,
        }));

    public Task<Result<CustomerEntitlementsReport, PolarError>> GetCustomerEntitlementsAsync(string customerId, CancellationToken ct = default)
        => Task.FromResult(ToResult(new CustomerEntitlementsReport
        {
            CustomerId = customerId,
            CustomerEmail = $"{customerId}@example.com",
        }));

    public Task<Result<string, PolarError>> GetTransactionsAsJsonAsync(TransactionReportRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult("{}"));

    public Task<Result<string, PolarError>> GetSubscriptionsAsJsonAsync(SubscriptionReportRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult("{}"));

    public Task<Result<string, PolarError>> GetOrdersAsJsonAsync(OrderReportRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult("{}"));

    public Task<Result<string, PolarError>> GetErrorAuditAsJsonAsync(ErrorAuditRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult("{}"));

    public Task<Result<string, PolarError>> GetCustomersAsJsonAsync(CustomerReportRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult("{}"));

    public Task<Result<PagedResult<CustomerListRow>, PolarError>> ListCustomersAsync(CustomerListRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult(CustomerListResponse));

    public Task<Result<PagedResult<OrderSummaryRow>, PolarError>> ListOrdersForCustomerAsync(string customerId, OrderListRequest request, CancellationToken ct = default)
        => Task.FromResult(ToResult(new PagedResult<OrderSummaryRow>
        {
            Rows = [],
            TotalCount = 0,
            Page = 0,
            PageSize = 50,
        }));

    public Task<Result<OrderDrilldownDetail, PolarError>> GetOrderDrilldownAsync(string orderId, CancellationToken ct = default)
        => Task.FromResult(ToResult(OrderDrilldownResponse));
}
