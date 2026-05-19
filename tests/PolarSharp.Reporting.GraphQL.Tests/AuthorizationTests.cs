using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.Reporting.GraphQL.Tests;

/// <summary>
/// Lock-down tests: every Query field is gated by an <c>[Authorize]</c> attribute, so an
/// unauthenticated request (or one whose <see cref="ICurrentUser"/> lacks the relevant
/// <see cref="PolarPermission"/>) must surface a GraphQL authorization error — not the
/// resolver's underlying data.
/// </summary>
public class AuthorizationTests
{
    [Fact]
    public async Task TransactionsQuery_Unauthenticated_ReturnsAuthorizationError()
    {
        var user = new StubCurrentUser(isAuthenticated: false, permissions: []);
        var (executor, _) = await TestExecutorFactory.CreateAsync(user: user).ConfigureAwait(true);

        var json = await EndToEndQueryTests.ExecuteAsync(executor,
            """
            query {
              transactions(input: {
                periodStart: "2026-01-01T00:00:00Z",
                periodEnd: "2026-02-01T00:00:00Z",
                currency: "USD"
              }) {
                grossRevenue
              }
            }
            """).ConfigureAwait(true);

        Assert.Contains("\"errors\"", json);
        // Hot Chocolate emits AUTH_NOT_AUTHORIZED / AUTH_NOT_AUTHENTICATED for [Authorize] denials.
        Assert.True(json.Contains("AUTH_NOT_AUTHORIZED", StringComparison.Ordinal)
            || json.Contains("AUTH_NOT_AUTHENTICATED", StringComparison.Ordinal),
            $"Expected an authorization error code, got: {json}");
    }

    [Fact]
    public async Task ErrorAuditQuery_WithoutAuditLogPermission_ReturnsAuthorizationError()
    {
        // User holds ViewReports but NOT ViewAuditLog; errorAudit requires the latter.
        var user = new StubCurrentUser(isAuthenticated: true,
            permissions: [PolarPermission.ViewReports]);
        var (executor, _) = await TestExecutorFactory.CreateAsync(user: user).ConfigureAwait(true);

        var json = await EndToEndQueryTests.ExecuteAsync(executor,
            """
            query {
              errorAudit(input: {
                periodStart: "2026-01-01T00:00:00Z",
                periodEnd: "2026-02-01T00:00:00Z"
              }) {
                webhookDeliveryFailures
              }
            }
            """).ConfigureAwait(true);

        Assert.Contains("\"errors\"", json);
        Assert.Contains("AUTH_NOT_AUTHORIZED", json);
    }

    [Fact]
    public async Task TransactionsQuery_WithViewReports_Succeeds()
    {
        var user = new StubCurrentUser(isAuthenticated: true,
            permissions: [PolarPermission.ViewReports]);
        var (executor, _) = await TestExecutorFactory.CreateAsync(user: user).ConfigureAwait(true);

        var json = await EndToEndQueryTests.ExecuteAsync(executor,
            """
            query {
              transactions(input: {
                periodStart: "2026-01-01T00:00:00Z",
                periodEnd: "2026-02-01T00:00:00Z",
                currency: "USD"
              }) {
                grossRevenue
              }
            }
            """).ConfigureAwait(true);

        Assert.DoesNotContain("\"errors\"", json);
        Assert.Contains("\"grossRevenue\"", json);
    }
}
