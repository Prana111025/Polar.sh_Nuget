using System.Security.Claims;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.Reporting;

namespace PolarSharp.Reporting.GraphQL.Tests;

/// <summary>
/// Stands up a Hot Chocolate <see cref="IRequestExecutor"/> against the PolarSharp.Reporting
/// schema with a <see cref="FakePolarReportingClient"/> backing the resolvers and a
/// <see cref="StubCurrentUser"/> driving the auth policy outcomes.
/// </summary>
internal sealed class StaticHttpContextAccessor : IHttpContextAccessor
{
    public StaticHttpContextAccessor(HttpContext? context) => HttpContext = context;
    public HttpContext? HttpContext { get; set; }
}

internal static class TestExecutorFactory
{
    public static async Task<(IRequestExecutor Executor, IServiceProvider Services)> CreateAsync(
        FakePolarReportingClient? client = null,
        StubCurrentUser? user = null)
    {
        var effectiveUser = user ?? new StubCurrentUser(isAuthenticated: true,
            permissions: [PolarPermission.ViewReports, PolarPermission.ViewAuditLog]);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPolarReportingClient>(client ?? new FakePolarReportingClient());
        services.AddScoped<ICurrentUser>(_ => effectiveUser);

        // Stand in for the host's HttpContextAccessor + IUserAccessor — the bridge looks the
        // principal up via IHttpContextAccessor. An authenticated ClaimsPrincipal is enough to
        // satisfy the .RequireAuthenticatedUser() the policies are registered with.
        var httpContext = effectiveUser.IsAuthenticated
            ? new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, effectiveUser.UserName ?? "test")],
                    authenticationType: "Test")),
            }
            : new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        services.AddSingleton<IHttpContextAccessor>(new StaticHttpContextAccessor(httpContext));

        services.AddScoped<IAuthorizationHandler, TestPermissionAuthorizationHandler>();

        services.AddPolarReportingGraphQL();

        var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequiredService<IRequestExecutorResolver>()
            .GetRequestExecutorAsync()
            .ConfigureAwait(false);
        return (executor, provider);
    }
}
