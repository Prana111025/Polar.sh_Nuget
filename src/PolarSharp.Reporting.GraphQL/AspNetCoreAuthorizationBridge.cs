using HotChocolate.Authorization;
using HotChocolate.Resolvers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using HotChocolateIAuthorizationHandler = HotChocolate.Authorization.IAuthorizationHandler;
using MicrosoftIAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;

namespace PolarSharp.Reporting.GraphQL;

/// <summary>
/// Bridges Hot Chocolate's <see cref="HotChocolateIAuthorizationHandler"/> protocol onto the
/// standard ASP.NET Core <see cref="MicrosoftIAuthorizationService"/>. Looks up the policy
/// referenced in each <see cref="AuthorizeDirective"/> via the registered
/// <c>IAuthorizationPolicyProvider</c> and delegates the actual permission check to
/// <c>IAuthorizationService.AuthorizeAsync</c> — which fans out to every registered
/// <see cref="Microsoft.AspNetCore.Authorization.IAuthorizationHandler"/>, including
/// PolarSharp's <c>PolarPermissionAuthorizationHandler</c>.
/// </summary>
/// <remarks>
/// <para>
/// This bridge exists because <c>HotChocolate.Authorization</c> 15.x does not ship a default
/// handler — the package leaves the actual policy evaluation to the host. By installing this
/// bridge inside <c>AddPolarReportingGraphQL</c>, REST and GraphQL share the same ASP.NET
/// Core policy + handler stack: there is exactly ONE place where <c>ViewReports</c> is
/// defined, ONE handler that evaluates it, and ONE source of truth (<c>ICurrentUser</c>) it
/// reads from.
/// </para>
/// <para>
/// When no <see cref="Microsoft.AspNetCore.Http.HttpContext"/> exists (e.g. unit tests
/// constructing the executor without a request), the bridge falls back to a synthetic
/// <see cref="System.Security.Claims.ClaimsPrincipal"/> with no identity — the policy handlers
/// still resolve <c>ICurrentUser</c> from DI, which is the only piece of state they actually
/// consult.
/// </para>
/// </remarks>
internal sealed class AspNetCoreAuthorizationBridge : HotChocolateIAuthorizationHandler
{
    public async ValueTask<AuthorizeResult> AuthorizeAsync(
        IMiddlewareContext context,
        AuthorizeDirective directive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(directive);

        return await EvaluateAsync(
                context.Services,
                directive.Policy,
                directive.Roles,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<AuthorizeResult> AuthorizeAsync(
        AuthorizationContext context,
        IReadOnlyList<AuthorizeDirective> directives,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(directives);

        foreach (var directive in directives)
        {
            var result = await EvaluateAsync(
                    context.Services,
                    directive.Policy,
                    directive.Roles,
                    cancellationToken)
                .ConfigureAwait(false);
            if (result is not AuthorizeResult.Allowed)
            {
                return result;
            }
        }

        return AuthorizeResult.Allowed;
    }

    private static async Task<AuthorizeResult> EvaluateAsync(
        IServiceProvider services,
        string? policyName,
        IReadOnlyList<string>? roles,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var authzService = services.GetService<MicrosoftIAuthorizationService>();
        if (authzService is null)
        {
            // No ASP.NET Core authorization service registered — the GraphQL [Authorize]
            // attribute has no way to evaluate the policy, so fail closed.
            return AuthorizeResult.NotAllowed;
        }

        var httpContextAccessor = services.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var principal = httpContextAccessor?.HttpContext?.User
            ?? new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());

        // Roles short-circuit (matches Hot Chocolate's documented [Authorize(Roles = ...)] semantics).
        if (roles is { Count: > 0 })
        {
            var hasAnyRole = roles.Any(principal.IsInRole);
            if (!hasAnyRole)
            {
                return AuthorizeResult.NotAllowed;
            }
        }

        if (string.IsNullOrEmpty(policyName))
        {
            // No policy — having the matching role (or no role requirement at all) is sufficient.
            // Still require an authenticated identity if no roles were specified.
            if (roles is null or { Count: 0 } && principal.Identity is not { IsAuthenticated: true })
            {
                return AuthorizeResult.NotAuthenticated;
            }
            return AuthorizeResult.Allowed;
        }

        var policyProvider = services.GetService<IAuthorizationPolicyProvider>();
        if (policyProvider is not null)
        {
            var policy = await policyProvider.GetPolicyAsync(policyName).ConfigureAwait(false);
            if (policy is null)
            {
                return AuthorizeResult.NoDefaultPolicy;
            }
        }

        var result = await authzService.AuthorizeAsync(principal, resource: null, policyName).ConfigureAwait(false);
        if (result.Succeeded)
        {
            return AuthorizeResult.Allowed;
        }

        return principal.Identity is { IsAuthenticated: true }
            ? AuthorizeResult.NotAllowed
            : AuthorizeResult.NotAuthenticated;
    }
}
