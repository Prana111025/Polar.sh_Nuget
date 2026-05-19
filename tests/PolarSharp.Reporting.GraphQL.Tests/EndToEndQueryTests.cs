using HotChocolate.Execution;

namespace PolarSharp.Reporting.GraphQL.Tests;

/// <summary>
/// End-to-end GraphQL query tests — feed canned values through
/// <see cref="FakePolarReportingClient"/> and assert the GraphQL response carries the same
/// values. Covers one aggregate query (transactions), one drilldown (listCustomers), one
/// nested-shape query (getOrderDrilldown with line items + benefit grants), and one
/// auth-gated query (errorAudit).
/// </summary>
public class EndToEndQueryTests
{
    [Fact]
    public async Task TransactionsQuery_ReturnsMappedValues()
    {
        var (executor, _) = await TestExecutorFactory.CreateAsync().ConfigureAwait(true);

        var json = await ExecuteAsync(executor,
            """
            query {
              transactions(input: {
                periodStart: "2026-01-01T00:00:00Z",
                periodEnd:   "2026-02-01T00:00:00Z",
                currency: "USD"
              }) {
                currency
                grossRevenue
                refundedAmount
                netRevenue
                orderCount
                averageOrderValue
              }
            }
            """).ConfigureAwait(true);

        Assert.Contains("\"currency\": \"USD\"", json);
        Assert.Contains("\"grossRevenue\": 100000", json);
        Assert.Contains("\"refundedAmount\": 5000", json);
        Assert.Contains("\"netRevenue\": 95000", json);
        Assert.Contains("\"orderCount\": 12", json);
        Assert.DoesNotContain("\"errors\"", json);
    }

    [Fact]
    public async Task ListCustomersQuery_ReturnsPagedRows()
    {
        var (executor, _) = await TestExecutorFactory.CreateAsync().ConfigureAwait(true);

        var json = await ExecuteAsync(executor,
            """
            query {
              listCustomers(input: { page: 0, pageSize: 50 }) {
                totalCount
                hasMore
                rows {
                  customerId
                  email
                  orderCount
                  lifetimeValue
                  currency
                }
              }
            }
            """).ConfigureAwait(true);

        Assert.Contains("\"totalCount\": 2", json);
        Assert.Contains("\"customerId\": \"cust_1\"", json);
        Assert.Contains("\"customerId\": \"cust_2\"", json);
        Assert.Contains("\"email\": \"first@example.com\"", json);
        Assert.Contains("\"lifetimeValue\": 25000", json);
        Assert.DoesNotContain("\"errors\"", json);
    }

    [Fact]
    public async Task GetOrderDrilldownQuery_ReturnsNestedShape()
    {
        var (executor, _) = await TestExecutorFactory.CreateAsync().ConfigureAwait(true);

        var json = await ExecuteAsync(executor,
            """
            query {
              orderDrilldown(orderId: "ord_test") {
                orderId
                orderNumber
                customerEmail
                status
                amount
                lineItems {
                  productId
                  productName
                  quantity
                  lineTotal
                }
                benefitGrants {
                  benefitId
                  benefitName
                  benefitKind
                  isGranted
                }
              }
            }
            """).ConfigureAwait(true);

        Assert.Contains("\"orderId\": \"ord_test\"", json);
        Assert.Contains("\"orderNumber\": \"INV-0001\"", json);
        Assert.Contains("\"productName\": \"Pro Plan\"", json);
        Assert.Contains("\"quantity\": 1", json);
        Assert.Contains("\"benefitKind\": \"license_keys\"", json);
        Assert.Contains("\"isGranted\": true", json);
        Assert.DoesNotContain("\"errors\"", json);
    }

    [Fact]
    public async Task ErrorAuditQuery_WithAuditLogPermission_ReturnsValues()
    {
        var (executor, _) = await TestExecutorFactory.CreateAsync().ConfigureAwait(true);

        var json = await ExecuteAsync(executor,
            """
            query {
              errorAudit(input: {
                periodStart: "2026-01-01T00:00:00Z",
                periodEnd: "2026-02-01T00:00:00Z"
              }) {
                webhookDeliveryFailures
                signatureVerificationFailures
                rateLimitHits
                apiErrorsByStatus
              }
            }
            """).ConfigureAwait(true);

        Assert.Contains("\"webhookDeliveryFailures\": 1", json);
        Assert.Contains("\"rateLimitHits\": 2", json);
        Assert.Contains("\"apiErrorsByStatus\": 3", json);
        Assert.DoesNotContain("\"errors\"", json);
    }

    [Fact]
    public async Task ReportingError_SurfacesAsGraphQLError()
    {
        var client = new FakePolarReportingClient
        {
            FailNext = new PolarSharp.NotFoundError("Customer not found", "req-xyz"),
        };
        var (executor, _) = await TestExecutorFactory.CreateAsync(client).ConfigureAwait(true);

        var json = await ExecuteAsync(executor,
            """
            query {
              customerEntitlements(customerId: "cust_missing") {
                customerId
              }
            }
            """).ConfigureAwait(true);

        Assert.Contains("\"errors\"", json);
        Assert.Contains("Customer not found", json);
        Assert.Contains("NotFoundError", json);
        Assert.Contains("req-xyz", json);
    }

    internal static async Task<string> ExecuteAsync(IRequestExecutor executor, string query)
    {
        var request = OperationRequestBuilder.New().SetDocument(query).Build();
        var response = await executor.ExecuteAsync(request).ConfigureAwait(false);
        return response.ToJson();
    }
}
