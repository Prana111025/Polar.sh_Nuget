using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant.Identity.Authorization;
using PolarSharp.Reporting.Drilldown;

namespace PolarSharp.Reporting.GraphQL;

/// <summary>
/// Hot Chocolate GraphQL schema registration for the PolarSharp Reporting read-side.
/// </summary>
/// <remarks>
/// <para>
/// Exposes the reporting drilldown (Customer → Orders → Order detail with line items + refunds
/// + benefit grants) plus aggregate KPI queries (Transactions / Subscriptions / Orders /
/// ErrorAudit / Customers / CustomerEntitlements) as a GraphQL schema that hosts can mount
/// alongside their existing REST endpoints. Hosts can mount the GraphQL endpoint at
/// <c>/graphql/reporting</c> (or any path of their choosing) and serve it to Strawberry Shake
/// typed clients OR Banana Cake Pop interactive UI in Development.
/// </para>
/// <para>
/// Every field is gated by a <c>PolarSharp.Permission.{PolarPermission}</c> ASP.NET Core
/// authorization policy. <see cref="AddPolarReportingGraphQL"/> idempotently registers the
/// per-permission policies via
/// <see cref="PolarAuthorizationPolicies.RegisterAllBuiltIn"/> so the GraphQL schema compiles
/// even when the host has not (yet) called <c>AddPolarIdentity</c>. The actual permission
/// <em>handlers</em> (<c>PolarPermissionAuthorizationHandler</c>) ship internal to
/// <c>PolarSharp.MultiTenant.Identity</c> and are wired by
/// <c>services.AddPolarIdentity(...)</c>. Hosts that mount the GraphQL endpoint without
/// calling <c>AddPolarIdentity</c> get fail-closed behaviour: the policy exists, no handler
/// returns <c>Succeed</c>, all GraphQL fields deny.
/// </para>
/// </remarks>
public static class ReportingGraphQLBuilderExtensions
{
    /// <summary>
    /// Registers Hot Chocolate GraphQL services for the PolarSharp Reporting schema.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>An <see cref="IRequestExecutorBuilder"/> for further GraphQL configuration.</returns>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddPolarReporting()
    ///     .UsePostgreSqlReporting(connStr);
    /// builder.Services.AddPolarIdentity(builder.Configuration);   // wires the auth handlers
    /// builder.Services.AddPolarReportingGraphQL();
    /// // ...
    /// app.MapGraphQL("/graphql/reporting");
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IRequestExecutorBuilder AddPolarReportingGraphQL(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Policies — idempotent: AddPolicy replaces the existing policy of the same name. The
        // ASP.NET Core registration (RegisterAllBuiltIn) installs one policy per PolarPermission
        // under the "PolarSharp.Permission.{name}" convention that the [Authorize] attributes
        // on PolarReportingQuery resolve.
        services.AddAuthorization(PolarAuthorizationPolicies.RegisterAllBuiltIn);

        return services
            .AddGraphQLServer()
            .AddAuthorizationCore()
            .AddAuthorizationHandler<AspNetCoreAuthorizationBridge>()
            .AddQueryType<PolarReportingQuery>()
            // PagedResult<T> closed generics need explicit registration — Hot Chocolate's
            // discovery walks input/output types referenced from resolvers, but generic
            // closures aren't always enumerable from the method signatures alone.
            .AddType<PagedResult<CustomerListRow>>()
            .AddType<PagedResult<OrderSummaryRow>>()
            .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = false);
    }
}
