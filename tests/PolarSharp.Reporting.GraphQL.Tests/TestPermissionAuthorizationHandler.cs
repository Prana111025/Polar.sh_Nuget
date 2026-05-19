using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using PolarSharp.MultiTenant.Identity;
using PolarSharp.MultiTenant.Identity.Authorization;

namespace PolarSharp.Reporting.GraphQL.Tests;

/// <summary>
/// Test handler that mirrors <c>PolarPermissionAuthorizationHandler</c>'s contract — it
/// consults the request-scoped <see cref="ICurrentUser"/> for the requested permission.
/// Reproduces the production semantics without crossing the internal-visibility boundary on
/// the real handler type.
/// </summary>
internal sealed class TestPermissionAuthorizationHandler : AuthorizationHandler<PolarPermissionRequirement>
{
    private readonly IServiceProvider _services;

    public TestPermissionAuthorizationHandler(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PolarPermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var user = _services.GetService<ICurrentUser>();
        if (user is null) return Task.CompletedTask;

        if (user.HasPermissionInCurrentTenant(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
