using Microsoft.EntityFrameworkCore;

namespace PolarSharp.MultiTenant.Identity.Tests.Integration;

/// <summary>
/// Test-only subclass of <see cref="PolarUserDbContext"/> that exposes the protected
/// tenant-aware constructor so integration tests can exercise the per-tenant query filter
/// on <see cref="PolarUserTenantMembership"/>.
/// </summary>
/// <remarks>
/// <para>
/// Production hosts get tenant context plumbed through the Finbuckle multi-tenant pipeline
/// (or through the host-DbContext shape where the host's own DbContext inherits
/// <c>TenantAwareDbContextBase</c>). For integration tests the simplest path is to
/// instantiate the context directly with a known tenant id, run the filter, and assert.
/// </para>
/// <para>
/// This subclass is deliberately confined to the test assembly's <c>Integration</c>
/// namespace — it must not leak into production wiring.
/// </para>
/// </remarks>
internal sealed class TenantScopedPolarUserDbContext : PolarUserDbContext
{
    /// <summary>Creates a new tenant-scoped context for tests.</summary>
    /// <param name="options">EF Core options (must be a <see cref="DbContextOptions{TContext}"/> of <see cref="PolarUserDbContext"/> because base Identity inherits from <see cref="Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext{TUser, TRole, TKey}"/> which keys options off the leaf type).</param>
    /// <param name="currentTenantId">Current tenant id — <see langword="null"/> means "no filter" (e.g., site-level operations).</param>
    /// <param name="isAppMasterAdminCrossTenant">When <see langword="true"/>, the query filter is bypassed regardless of tenant id.</param>
    public TenantScopedPolarUserDbContext(
        DbContextOptions<PolarUserDbContext> options,
        Guid? currentTenantId,
        bool isAppMasterAdminCrossTenant)
        : base(options, currentTenantId, isAppMasterAdminCrossTenant)
    {
    }
}
