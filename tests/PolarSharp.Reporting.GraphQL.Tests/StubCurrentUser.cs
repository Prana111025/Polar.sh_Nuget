using PolarSharp.MultiTenant.Identity;

namespace PolarSharp.Reporting.GraphQL.Tests;

/// <summary>
/// Pre-canned <see cref="ICurrentUser"/> for GraphQL authorization tests. Toggles between
/// "authenticated with ViewReports + ViewAuditLog" (the default green-path state) and
/// "unauthenticated" (the regression test for the unauth schema).
/// </summary>
internal sealed class StubCurrentUser : ICurrentUser
{
    public StubCurrentUser(bool isAuthenticated, IReadOnlyList<PolarPermission> permissions)
    {
        IsAuthenticated = isAuthenticated;
        CurrentPermissions = permissions;
        UserId = isAuthenticated ? Guid.NewGuid() : null;
        CurrentTenantId = isAuthenticated ? Guid.NewGuid() : null;
    }

    public Guid? UserId { get; }
    public string? Email => IsAuthenticated ? "test@example.com" : null;
    public string? UserName => IsAuthenticated ? "test-user" : null;
    public Guid? CurrentTenantId { get; }
    public bool IsAppMasterAdmin => false;
    public bool IsAuthenticated { get; }
    public IReadOnlyList<string> CurrentRoles => IsAuthenticated ? ["TenantOperator"] : [];
    public IReadOnlyList<PolarPermission> CurrentPermissions { get; }

    public bool IsInRoleForCurrentTenant(string role) => CurrentRoles.Contains(role);

    public bool HasPermissionInCurrentTenant(PolarPermission permission) =>
        CurrentPermissions.Contains(permission);
}
